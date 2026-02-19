using Railbird.Core.Hrs.Models;

namespace Railbird.Core.Rules;

public enum ActionKind
{
    Check,
    Fold,
    Call,
    Bet,
    Raise,
    AllIn
}

public sealed class NlheGameState
{
    private readonly List<int> _seats;
    private readonly HashSet<int> _folded;
    private readonly HashSet<int> _allIn;
    private List<int> _actionOrder;
    private int _actionIndex;
    private HashSet<int> _pending;

    public IReadOnlyList<int> Seats => _seats;
    public Dictionary<int, decimal> Stacks { get; }
    public Dictionary<int, decimal> Committed { get; }
    public Street CurrentStreet { get; private set; }
    public decimal CurrentBet { get; private set; }
    public decimal LastRaiseSize { get; private set; }
    public int ButtonSeat { get; }
    public int SmallBlindSeat { get; }
    public int BigBlindSeat { get; }
    public decimal SmallBlind { get; }
    public decimal BigBlind { get; }
    public bool IsHandOver { get; private set; }
    public bool NeedsBoardDeal { get; private set; }
    public IReadOnlyCollection<int> Pending => _pending;

    public NlheGameState(List<int> seats, int buttonSeat, decimal sb, decimal bb, Dictionary<int, decimal> startingStacks)
    {
        _seats = seats;
        ButtonSeat = buttonSeat;
        SmallBlind = sb;
        BigBlind = bb;

        if (seats.Count == 2)
        {
            SmallBlindSeat = buttonSeat;
            BigBlindSeat = NextSeat(buttonSeat);
        }
        else
        {
            SmallBlindSeat = NextSeat(buttonSeat);
            BigBlindSeat = NextSeat(SmallBlindSeat);
        }

        Stacks = startingStacks.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        Committed = seats.ToDictionary(seat => seat, _ => 0m);
        _folded = new HashSet<int>();
        _allIn = new HashSet<int>();
        _actionOrder = new List<int>();
        _pending = new HashSet<int>();
        _actionIndex = 0;
        CurrentStreet = Street.PREFLOP;
    }

    public void PostBlind(int seat, decimal amount)
    {
        var contribution = Math.Min(amount, Stacks[seat]);
        Stacks[seat] -= contribution;
        Committed[seat] += contribution;
        if (Stacks[seat] == 0m)
        {
            _allIn.Add(seat);
        }
    }

    public void StartPreflop()
    {
        CurrentStreet = Street.PREFLOP;
        CurrentBet = BigBlind;
        LastRaiseSize = BigBlind;
        NeedsBoardDeal = false;
        _pending = new HashSet<int>(EligibleToAct());
        _actionOrder = BuildActionOrder(CurrentStreet);
        _actionIndex = 0;
    }

    public bool ShouldAdvanceStreet()
    {
        return _pending.Count == 0 && !IsHandOver && !NeedsBoardDeal;
    }

    public bool AdvanceStreet()
    {
        if (NonFoldedCount() <= 1)
        {
            IsHandOver = true;
            return false;
        }

        if (CurrentStreet == Street.RIVER)
        {
            IsHandOver = true;
            return false;
        }

        CurrentStreet = CurrentStreet + 1;
        foreach (var seat in _seats)
        {
            Committed[seat] = 0m;
        }

        CurrentBet = 0m;
        LastRaiseSize = BigBlind;
        NeedsBoardDeal = true;
        _pending = new HashSet<int>(EligibleToAct());
        _actionOrder = BuildActionOrder(CurrentStreet);
        _actionIndex = 0;
        return true;
    }

    public void MarkBoardDealt()
    {
        NeedsBoardDeal = false;
    }

    public int? GetNextActor()
    {
        if (_pending.Count == 0)
        {
            return null;
        }

        for (var i = 0; i < _actionOrder.Count; i++)
        {
            var index = (_actionIndex + i) % _actionOrder.Count;
            var seat = _actionOrder[index];
            if (_pending.Contains(seat))
            {
                _actionIndex = (index + 1) % _actionOrder.Count;
                return seat;
            }
        }

        return null;
    }

    public IReadOnlyList<ActionKind> GetLegalActions(int seat)
    {
        var options = new List<ActionKind>();
        var stack = Stacks[seat];
        var committed = Committed[seat];

        if (CurrentBet == 0m)
        {
            options.Add(ActionKind.Check);
            if (stack > 0m)
            {
                if (stack >= BigBlind)
                {
                    options.Add(ActionKind.Bet);
                }
                options.Add(ActionKind.AllIn);
            }
        }
        else
        {
            options.Add(ActionKind.Fold);
            if (stack > 0m)
            {
                options.Add(ActionKind.Call);
                if (committed + stack > CurrentBet)
                {
                    options.Add(ActionKind.Raise);
                }
                options.Add(ActionKind.AllIn);
            }
        }

        return options;
    }

    public decimal GetCallAmount(int seat)
    {
        return Math.Max(0m, CurrentBet - Committed[seat]);
    }

    public decimal GetMinRaiseTo()
    {
        return CurrentBet + LastRaiseSize;
    }

    public decimal GetMaxRaiseTo(int seat)
    {
        return Committed[seat] + Stacks[seat];
    }

    public void ApplyCheck(int seat)
    {
        _pending.Remove(seat);
    }

    public void ApplyFold(int seat)
    {
        _folded.Add(seat);
        _pending.Remove(seat);
        if (NonFoldedCount() <= 1)
        {
            IsHandOver = true;
        }
    }

    public void ApplyCall(int seat, decimal contribution, bool isAllIn = false)
    {
        Stacks[seat] -= contribution;
        Committed[seat] += contribution;
        if (isAllIn || Stacks[seat] == 0m)
        {
            _allIn.Add(seat);
        }
        _pending.Remove(seat);
    }

    public void ApplyBet(int seat, decimal amount, bool isAllIn = false)
    {
        Stacks[seat] -= amount;
        Committed[seat] += amount;
        CurrentBet = amount;
        LastRaiseSize = amount;
        if (isAllIn || Stacks[seat] == 0m)
        {
            _allIn.Add(seat);
        }
        ResetPendingAfterAggression(seat);
    }

    public void ApplyRaise(int seat, decimal toAmount, decimal contribution, bool isAllIn = false)
    {
        var oldBet = CurrentBet;
        Stacks[seat] -= contribution;
        Committed[seat] += contribution;
        CurrentBet = toAmount;

        var reopen = toAmount >= oldBet + LastRaiseSize;
        if (reopen)
        {
            LastRaiseSize = toAmount - oldBet;
            ResetPendingAfterAggression(seat);
        }
        else
        {
            _pending.Remove(seat);
        }

        if (isAllIn || Stacks[seat] == 0m)
        {
            _allIn.Add(seat);
        }
    }

    private void ResetPendingAfterAggression(int aggressorSeat)
    {
        _pending = new HashSet<int>(EligibleToAct());
        _pending.Remove(aggressorSeat);
    }

    private IEnumerable<int> EligibleToAct()
    {
        foreach (var seat in _seats)
        {
            if (_folded.Contains(seat))
            {
                continue;
            }

            if (_allIn.Contains(seat) || Stacks[seat] <= 0m)
            {
                continue;
            }

            yield return seat;
        }
    }

    private List<int> BuildActionOrder(Street street)
    {
        var first = GetFirstActor(street);
        var order = new List<int>();
        var startIndex = _seats.IndexOf(first);
        for (var i = 0; i < _seats.Count; i++)
        {
            var seat = _seats[(startIndex + i) % _seats.Count];
            if (_folded.Contains(seat))
            {
                continue;
            }
            order.Add(seat);
        }
        return order;
    }

    private int GetFirstActor(Street street)
    {
        if (street == Street.PREFLOP)
        {
            if (_seats.Count == 2)
            {
                return ButtonSeat;
            }

            return NextSeat(BigBlindSeat);
        }

        if (_seats.Count == 2)
        {
            return BigBlindSeat;
        }

        return SmallBlindSeat;
    }

    private int NextSeat(int seat)
    {
        var index = _seats.IndexOf(seat);
        return _seats[(index + 1) % _seats.Count];
    }

    private int NonFoldedCount()
    {
        return _seats.Count(seat => !_folded.Contains(seat));
    }
}

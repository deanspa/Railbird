using Railbird.Core.Rules;
using Xunit;

namespace Railbird.Core.Tests;

public sealed class NlheRulesTests
{
    [Fact]
    public void PreflopOrderSixMaxStartsUnderTheGun()
    {
        var seats = new List<int> { 1, 2, 3, 4, 5, 6 };
        var state = CreateState(seats, buttonSeat: 1, sb: 0.5m, bb: 1m);

        state.PostBlind(state.SmallBlindSeat, 0.5m);
        state.PostBlind(state.BigBlindSeat, 1m);
        state.StartPreflop();

        var nextActor = state.GetNextActor();
        Assert.Equal(4, nextActor);
    }

    [Fact]
    public void HeadsUpUsesButtonAsSmallBlindAndFirstToActPreflop()
    {
        var seats = new List<int> { 1, 2 };
        var state = CreateState(seats, buttonSeat: 1, sb: 0.5m, bb: 1m);

        Assert.Equal(1, state.SmallBlindSeat);
        Assert.Equal(2, state.BigBlindSeat);

        state.PostBlind(state.SmallBlindSeat, 0.5m);
        state.PostBlind(state.BigBlindSeat, 1m);
        state.StartPreflop();

        var nextActor = state.GetNextActor();
        Assert.Equal(1, nextActor);
    }

    [Fact]
    public void ShortAllInRaiseDoesNotReopenAction()
    {
        var seats = new List<int> { 1, 2, 3 };
        var state = CreateState(seats, buttonSeat: 1, sb: 1m, bb: 2m);

        state.PostBlind(state.SmallBlindSeat, 1m);
        state.PostBlind(state.BigBlindSeat, 2m);
        state.StartPreflop();

        Assert.Equal(1, state.GetNextActor());
        state.ApplyRaise(1, toAmount: 6m, contribution: 6m);

        Assert.Equal(2, state.GetNextActor());
        var sbCall = state.GetCallAmount(2);
        state.ApplyCall(2, sbCall);

        Assert.Equal(3, state.GetNextActor());
        state.ApplyRaise(3, toAmount: 8m, contribution: 6m, isAllIn: true);

        Assert.Equal(4m, state.LastRaiseSize);
        Assert.True(state.ShouldAdvanceStreet());
    }

    [Fact]
    public void LegalActionsIncludeCallRaiseWhenFacingBet()
    {
        var seats = new List<int> { 1, 2, 3 };
        var state = CreateState(seats, buttonSeat: 1, sb: 1m, bb: 2m);

        state.PostBlind(state.SmallBlindSeat, 1m);
        state.PostBlind(state.BigBlindSeat, 2m);
        state.StartPreflop();

        var actions = state.GetLegalActions(2);
        Assert.Contains(ActionKind.Fold, actions);
        Assert.Contains(ActionKind.Call, actions);
        Assert.Contains(ActionKind.Raise, actions);
        Assert.Contains(ActionKind.AllIn, actions);
    }

    private static NlheGameState CreateState(List<int> seats, int buttonSeat, decimal sb, decimal bb)
    {
        var stacks = seats.ToDictionary(seat => seat, _ => 100m);
        return new NlheGameState(seats, buttonSeat, sb, bb, stacks);
    }
}

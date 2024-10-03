#region

using Serilog;
using Stateless;

#endregion

namespace DropBear.Codex.StateManagement.StateMachine.Builder;

/// <summary>
///     A fluent builder for configuring a state machine.
/// </summary>
/// <typeparam name="TState">The type of the state.</typeparam>
/// <typeparam name="TTrigger">The type of the trigger.</typeparam>
public class StateMachineBuilder<TState, TTrigger>
{
    private readonly ILogger _logger;
    private readonly Lazy<StateMachine<TState, TTrigger>> _stateMachine;
    private bool _isBuilt;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StateMachineBuilder{TState,TTrigger}" /> class.
    /// </summary>
    /// <param name="initialState">The initial state of the state machine.</param>
    /// <param name="logger">The logger instance for logging state machine configuration.</param>
    public StateMachineBuilder(TState initialState, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(initialState, nameof(initialState));
        _stateMachine =
            new Lazy<StateMachine<TState, TTrigger>>(() => new StateMachine<TState, TTrigger>(initialState));
        _logger = logger ?? Log.Logger.ForContext<StateMachineBuilder<TState, TTrigger>>();
        _logger.Debug("State machine initialized with initial state: {State}", initialState);
    }

    /// <summary>
    ///     Configures the specified state.
    /// </summary>
    /// <param name="state">The state to configure.</param>
    /// <returns>The state configuration for further chaining.</returns>
    public StateMachine<TState, TTrigger>.StateConfiguration ConfigureState(TState state)
    {
        EnsureNotBuilt();
        ArgumentNullException.ThrowIfNull(state, nameof(state));
        _logger.Debug("Configuring state: {State}", state);
        return _stateMachine.Value.Configure(state);
    }

    /// <summary>
    ///     Configures the state machine to permit transitioning from the specified state to the destination state when the
    ///     specified trigger is fired.
    /// </summary>
    /// <param name="state">The state to configure.</param>
    /// <param name="trigger">The trigger that causes the transition.</param>
    /// <param name="destinationState">The destination state.</param>
    /// <returns>The state machine builder for fluent chaining.</returns>
    public StateMachineBuilder<TState, TTrigger> Permit(TState state, TTrigger trigger, TState destinationState)
    {
        EnsureNotBuilt();
        ArgumentNullException.ThrowIfNull(state, nameof(state));
        ArgumentNullException.ThrowIfNull(trigger, nameof(trigger));
        ArgumentNullException.ThrowIfNull(destinationState, nameof(destinationState));

        _logger.Debug("Configuring transition: {State} --({Trigger})--> {DestinationState}",
            state, trigger, destinationState);

        _stateMachine.Value.Configure(state).Permit(trigger, destinationState);
        return this;
    }

    /// <summary>
    ///     Configures the state machine to permit reentry into the specified state when the specified trigger is fired.
    /// </summary>
    /// <param name="state">The state to configure.</param>
    /// <param name="trigger">The trigger that causes the reentry.</param>
    /// <returns>The state machine builder for fluent chaining.</returns>
    public StateMachineBuilder<TState, TTrigger> PermitReentry(TState state, TTrigger trigger)
    {
        EnsureNotBuilt();
        ArgumentNullException.ThrowIfNull(state, nameof(state));
        ArgumentNullException.ThrowIfNull(trigger, nameof(trigger));

        _logger.Debug("Configuring reentry for state: {State} on trigger: {Trigger}", state, trigger);

        _stateMachine.Value.Configure(state).PermitReentry(trigger);
        return this;
    }

    /// <summary>
    ///     Configures the state machine to permit reentry into the specified state when the specified trigger is fired,
    ///     executing the specified action.
    /// </summary>
    /// <param name="state">The state to configure.</param>
    /// <param name="trigger">The trigger that causes the reentry.</param>
    /// <param name="action">The action to execute during reentry.</param>
    /// <returns>The state machine builder for fluent chaining.</returns>
    public StateMachineBuilder<TState, TTrigger> PermitReentry(TState state, TTrigger trigger, Action action)
    {
        EnsureNotBuilt();
        ArgumentNullException.ThrowIfNull(state, nameof(state));
        ArgumentNullException.ThrowIfNull(trigger, nameof(trigger));
        ArgumentNullException.ThrowIfNull(action, nameof(action));

        _logger.Debug("Configuring reentry for state: {State} on trigger: {Trigger} with action.", state,
            trigger);

        _stateMachine.Value.Configure(state).PermitReentry(trigger).OnEntry(action);
        return this;
    }

    /// <summary>
    ///     Configures the state machine to ignore the specified trigger when in the specified state.
    /// </summary>
    /// <param name="state">The state to configure.</param>
    /// <param name="trigger">The trigger to ignore.</param>
    /// <returns>The state machine builder for fluent chaining.</returns>
    public StateMachineBuilder<TState, TTrigger> Ignore(TState state, TTrigger trigger)
    {
        EnsureNotBuilt();
        ArgumentNullException.ThrowIfNull(state, nameof(state));
        ArgumentNullException.ThrowIfNull(trigger, nameof(trigger));

        _logger.Debug("Configuring state: {State} to ignore trigger: {Trigger}", state, trigger);

        _stateMachine.Value.Configure(state).Ignore(trigger);
        return this;
    }

    /// <summary>
    ///     Configures the state machine to ignore the specified trigger when in the specified state, if the specified guard
    ///     condition is met.
    /// </summary>
    /// <param name="state">The state to configure.</param>
    /// <param name="trigger">The trigger to ignore.</param>
    /// <param name="guard">The guard condition that must be met for the trigger to be ignored.</param>
    /// <param name="guardDescription">The description of the guard condition.</param>
    /// <returns>The state machine builder for fluent chaining.</returns>
    public StateMachineBuilder<TState, TTrigger> IgnoreIf(TState state, TTrigger trigger, Func<bool> guard,
        string? guardDescription = null)
    {
        EnsureNotBuilt();
        ArgumentNullException.ThrowIfNull(state, nameof(state));
        ArgumentNullException.ThrowIfNull(trigger, nameof(trigger));
        ArgumentNullException.ThrowIfNull(guard, nameof(guard));

        _logger.Debug(
            "Configuring state: {State} to ignore trigger: {Trigger} if condition: {GuardDescription} is met.",
            state, trigger, guardDescription ?? "Unnamed guard");

        _stateMachine.Value.Configure(state).IgnoreIf(trigger, guard, guardDescription);
        return this;
    }

    /// <summary>
    ///     Configures the state machine to ignore the specified trigger when in the specified state, if any of the specified
    ///     guard conditions are met.
    /// </summary>
    /// <param name="state">The state to configure.</param>
    /// <param name="trigger">The trigger to ignore.</param>
    /// <param name="guards">The guard conditions that must be met for the trigger to be ignored.</param>
    /// <returns>The state machine builder for fluent chaining.</returns>
    public StateMachineBuilder<TState, TTrigger> IgnoreIf(TState state, TTrigger trigger,
        params Tuple<Func<bool>, string>[] guards)
    {
        EnsureNotBuilt();
        ArgumentNullException.ThrowIfNull(state, nameof(state));
        ArgumentNullException.ThrowIfNull(trigger, nameof(trigger));
        ArgumentNullException.ThrowIfNull(guards, nameof(guards));

        _logger.Debug("Configuring state: {State} to ignore trigger: {Trigger} if any guard conditions are met.",
            state, trigger);

        _stateMachine.Value.Configure(state).IgnoreIf(trigger, guards);
        return this;
    }

    /// <summary>
    ///     Configures the state machine to execute the specified action when entering the specified state.
    /// </summary>
    /// <param name="state">The state to configure.</param>
    /// <param name="entryAction">The action to execute when entering the state.</param>
    /// <returns>The state machine builder for fluent chaining.</returns>
    public StateMachineBuilder<TState, TTrigger> OnEntry(TState state, Action entryAction)
    {
        EnsureNotBuilt();
        ArgumentNullException.ThrowIfNull(state, nameof(state));
        ArgumentNullException.ThrowIfNull(entryAction, nameof(entryAction));

        _logger.Debug("Configuring state: {State} to execute entry action.", state);

        _stateMachine.Value.Configure(state).OnEntry(entryAction);
        return this;
    }

    /// <summary>
    ///     Configures the state machine to execute the specified action when entering the specified state.
    /// </summary>
    /// <param name="state">The state to configure.</param>
    /// <param name="entryAction">The action to execute when entering the state.</param>
    /// <param name="entryActionDescription">The description of the entry action.</param>
    /// <returns>The state machine builder for fluent chaining.</returns>
    public StateMachineBuilder<TState, TTrigger> OnEntry(TState state,
        Action<StateMachine<TState, TTrigger>.Transition> entryAction, string? entryActionDescription = null)
    {
        EnsureNotBuilt();
        ArgumentNullException.ThrowIfNull(state, nameof(state));
        ArgumentNullException.ThrowIfNull(entryAction, nameof(entryAction));

        _logger.Debug("Configuring state: {State} to execute entry action: {EntryActionDescription}.",
            state, entryActionDescription ?? "Unnamed action");

        _stateMachine.Value.Configure(state).OnEntry(entryAction, entryActionDescription);
        return this;
    }

    /// <summary>
    ///     Configures the state machine to execute the specified action when exiting the specified state.
    /// </summary>
    /// <param name="state">The state to configure.</param>
    /// <param name="exitAction">The action to execute when exiting the state.</param>
    /// <returns>The state machine builder for fluent chaining.</returns>
    public StateMachineBuilder<TState, TTrigger> OnExit(TState state, Action exitAction)
    {
        EnsureNotBuilt();
        ArgumentNullException.ThrowIfNull(state, nameof(state));
        ArgumentNullException.ThrowIfNull(exitAction, nameof(exitAction));

        _logger.Debug("Configuring state: {State} to execute exit action.", state);

        _stateMachine.Value.Configure(state).OnExit(exitAction);
        return this;
    }

    /// <summary>
    ///     Configures the state machine to execute the specified action when exiting the specified state.
    /// </summary>
    /// <param name="state">The state to configure.</param>
    /// <param name="exitAction">The action to execute when exiting the state.</param>
    /// <param name="exitActionDescription">The description of the exit action.</param>
    /// <returns>The state machine builder for fluent chaining.</returns>
    public StateMachineBuilder<TState, TTrigger> OnExit(TState state,
        Action<StateMachine<TState, TTrigger>.Transition> exitAction, string? exitActionDescription = null)
    {
        EnsureNotBuilt();
        ArgumentNullException.ThrowIfNull(state, nameof(state));
        ArgumentNullException.ThrowIfNull(exitAction, nameof(exitAction));

        _logger.Debug("Configuring state: {State} to execute exit action: {ExitActionDescription}.",
            state, exitActionDescription ?? "Unnamed action");

        _stateMachine.Value.Configure(state).OnExit(exitAction, exitActionDescription);
        return this;
    }

    /// <summary>
    ///     Configures the specified state as a substate of the specified superstate.
    /// </summary>
    /// <param name="state">The substate to configure.</param>
    /// <param name="superstate">The superstate.</param>
    /// <returns>The state machine builder for fluent chaining.</returns>
    public StateMachineBuilder<TState, TTrigger> SubstateOf(TState state, TState superstate)
    {
        EnsureNotBuilt();
        ArgumentNullException.ThrowIfNull(state, nameof(state));
        ArgumentNullException.ThrowIfNull(superstate, nameof(superstate));

        _logger.Debug("Configuring state: {State} as a substate of {Superstate}.", state, superstate);

        _stateMachine.Value.Configure(state).SubstateOf(superstate);
        return this;
    }

    /// <summary>
    ///     Configures the state machine with an internal transition for the specified state and trigger, executing the
    ///     specified action.
    /// </summary>
    /// <param name="state">The state to configure.</param>
    /// <param name="trigger">The trigger that causes the internal transition.</param>
    /// <param name="transitionAction">The action to execute during the internal transition.</param>
    /// <returns>The state machine builder for fluent chaining.</returns>
    public StateMachineBuilder<TState, TTrigger> InternalTransition(TState state, TTrigger trigger,
        Action<StateMachine<TState, TTrigger>.Transition> transitionAction)
    {
        EnsureNotBuilt();
        ArgumentNullException.ThrowIfNull(state, nameof(state));
        ArgumentNullException.ThrowIfNull(trigger, nameof(trigger));
        ArgumentNullException.ThrowIfNull(transitionAction, nameof(transitionAction));

        _logger.Debug("Configuring internal transition for state: {State} on trigger: {Trigger}.",
            state, trigger);

        _stateMachine.Value.Configure(state).InternalTransition(trigger, transitionAction);
        return this;
    }

    /// <summary>
    ///     Configures the state machine to permit transitioning from the specified state to the destination state when the
    ///     specified trigger is fired, if the specified guard condition is met.
    /// </summary>
    /// <param name="state">The state to configure.</param>
    /// <param name="trigger">The trigger that causes the transition.</param>
    /// <param name="destinationState">The destination state.</param>
    /// <param name="guard">The guard condition that must be met for the transition to occur.</param>
    /// <param name="guardDescription">The description of the guard condition.</param>
    /// <returns>The state machine builder for fluent chaining.</returns>
    public StateMachineBuilder<TState, TTrigger> PermitIf(TState state, TTrigger trigger, TState destinationState,
        Func<bool> guard, string? guardDescription = null)
    {
        EnsureNotBuilt();
        ArgumentNullException.ThrowIfNull(state, nameof(state));
        ArgumentNullException.ThrowIfNull(trigger, nameof(trigger));
        ArgumentNullException.ThrowIfNull(destinationState, nameof(destinationState));
        ArgumentNullException.ThrowIfNull(guard, nameof(guard));

        _logger.Debug(
            "Configuring transition for state: {State} to {DestinationState} on trigger: {Trigger} if condition: {GuardDescription} is met.",
            state, destinationState, trigger, guardDescription ?? "Unnamed guard");

        _stateMachine.Value.Configure(state).PermitIf(trigger, destinationState, guard, guardDescription);
        return this;
    }

    /// <summary>
    ///     Configures the state machine to permit transitioning from the specified state to the destination state when the
    ///     specified trigger is fired, if all of the specified guard conditions are met.
    /// </summary>
    /// <param name="state">The state to configure.</param>
    /// <param name="trigger">The trigger that causes the transition.</param>
    /// <param name="destinationState">The destination state.</param>
    /// <param name="guards">The guard conditions that must be met for the transition to occur.</param>
    /// <returns>The state machine builder for fluent chaining.</returns>
    public StateMachineBuilder<TState, TTrigger> PermitIf(TState state, TTrigger trigger, TState destinationState,
        params Tuple<Func<bool>, string>[] guards)
    {
        EnsureNotBuilt();
        ArgumentNullException.ThrowIfNull(state, nameof(state));
        ArgumentNullException.ThrowIfNull(trigger, nameof(trigger));
        ArgumentNullException.ThrowIfNull(destinationState, nameof(destinationState));
        ArgumentNullException.ThrowIfNull(guards, nameof(guards));

        _logger.Debug(
            "Configuring transition for state: {State} to {DestinationState} on trigger: {Trigger} if multiple guard conditions are met.",
            state, destinationState, trigger);

        _stateMachine.Value.Configure(state).PermitIf(trigger, destinationState, guards);
        return this;
    }

    /// <summary>
    ///     Configures the state machine with an initial transition to the specified state when entering the specified state.
    /// </summary>
    /// <param name="state">The state to configure.</param>
    /// <param name="initialState">The initial state to transition to.</param>
    /// <returns>The state machine builder for fluent chaining.</returns>
    public StateMachineBuilder<TState, TTrigger> InitialTransition(TState state, TState initialState)
    {
        EnsureNotBuilt();
        ArgumentNullException.ThrowIfNull(state, nameof(state));
        ArgumentNullException.ThrowIfNull(initialState, nameof(initialState));

        _logger.Debug("Configuring initial transition for state: {State} to {InitialState}.", state,
            initialState);

        _stateMachine.Value.Configure(state).InitialTransition(initialState);
        return this;
    }

    /// <summary>
    ///     Configures the state machine to execute the specified action when entering the specified state via the specified
    ///     trigger.
    /// </summary>
    /// <param name="state">The state to configure.</param>
    /// <param name="trigger">The trigger that causes the transition.</param>
    /// <param name="entryAction">The action to execute when entering the state via the specified trigger.</param>
    /// <param name="entryActionDescription">The description of the entry action.</param>
    /// <returns>The state machine builder for fluent chaining.</returns>
    public StateMachineBuilder<TState, TTrigger> OnEntryFrom(TState state, TTrigger trigger,
        Action<StateMachine<TState, TTrigger>.Transition> entryAction, string? entryActionDescription = null)
    {
        EnsureNotBuilt();
        ArgumentNullException.ThrowIfNull(state, nameof(state));
        ArgumentNullException.ThrowIfNull(trigger, nameof(trigger));
        ArgumentNullException.ThrowIfNull(entryAction, nameof(entryAction));

        _logger.Debug("Configuring OnEntryFrom action for state: {State} triggered by {Trigger}.", state,
            trigger);

        _stateMachine.Value.Configure(state).OnEntryFrom(trigger, entryAction, entryActionDescription);
        return this;
    }

    /// <summary>
    ///     Builds and returns the configured state machine.
    /// </summary>
    /// <returns>The configured state machine.</returns>
    public StateMachine<TState, TTrigger> Build()
    {
        if (_isBuilt)
        {
            throw new InvalidOperationException("State machine has already been built.");
        }

        _logger.Debug("State machine build complete.");
        _isBuilt = true;
        return _stateMachine.Value;
    }

    /// <summary>
    ///     Ensures that the state machine has not been built before allowing further configuration.
    /// </summary>
    private void EnsureNotBuilt()
    {
        if (_isBuilt)
        {
            throw new InvalidOperationException("Cannot configure a state machine after it has been built.");
        }
    }
}

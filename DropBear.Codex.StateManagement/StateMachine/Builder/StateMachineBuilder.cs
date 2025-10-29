#region

using DropBear.Codex.Core.Results;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.StateManagement.Errors;
using Serilog;
using Stateless;

#endregion

namespace DropBear.Codex.StateManagement.StateMachine.Builder;

/// <summary>
///     A fluent builder for configuring a <see cref="StateMachine{TState,TTrigger}" />.
///     Allows adding states, triggers, transitions, and actions in a fluent manner
///     before finally constructing the state machine.
/// </summary>
/// <typeparam name="TState">The enumeration or class representing the states.</typeparam>
/// <typeparam name="TTrigger">The enumeration or class representing the triggers.</typeparam>
public class StateMachineBuilder<TState, TTrigger>
{
    private readonly ILogger _logger;
    private readonly Lazy<StateMachine<TState, TTrigger>> _stateMachine;
    private bool _isBuilt;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StateMachineBuilder{TState, TTrigger}" /> class.
    /// </summary>
    /// <param name="initialState">The initial state of the state machine. Must not be null.</param>
    /// <param name="logger">
    ///     An optional <see cref="ILogger" /> for logging state machine configuration. If null, a default logger is used.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="initialState" /> is null.</exception>
    public StateMachineBuilder(TState initialState, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(initialState, nameof(initialState));

        // Lazy instantiation of the underlying Stateless state machine
        _stateMachine =
            new Lazy<StateMachine<TState, TTrigger>>(() => new StateMachine<TState, TTrigger>(initialState));

        // If no logger provided, create a default for this type
        _logger = logger ?? Log.Logger.ForContext<StateMachineBuilder<TState, TTrigger>>();
        _logger.Debug("State machine initialized with initial state: {State}", initialState);
    }

    /// <summary>
    ///     Configures the specified <paramref name="state" />, returning its
    ///     <see cref="StateMachine{TState, TTrigger}.StateConfiguration" />.
    ///     Further actions (e.g., Permit, OnEntry) can be chained on the returned configuration.
    /// </summary>
    /// <param name="state">The state to configure.</param>
    /// <returns>The <see cref="StateMachine{TState, TTrigger}.StateConfiguration" /> for further chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the builder has already been built.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="state" /> is null.</exception>
    public StateMachine<TState, TTrigger>.StateConfiguration ConfigureState(TState state)
    {
        EnsureNotBuilt();
        ArgumentNullException.ThrowIfNull(state, nameof(state));

        _logger.Debug("Configuring state: {State}", state);
        return _stateMachine.Value.Configure(state);
    }

    /// <summary>
    ///     Configures a simple transition from <paramref name="state" /> to <paramref name="destinationState" />
    ///     when <paramref name="trigger" /> is fired.
    /// </summary>
    /// <param name="state">The source state.</param>
    /// <param name="trigger">The trigger causing the transition.</param>
    /// <param name="destinationState">The target state.</param>
    /// <returns>This <see cref="StateMachineBuilder{TState, TTrigger}" /> for fluent chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the builder has already been built.</exception>
    /// <exception cref="ArgumentNullException">Thrown if any parameter is null.</exception>
    public StateMachineBuilder<TState, TTrigger> Permit(TState state, TTrigger trigger, TState destinationState)
    {
        EnsureNotBuilt();
        ArgumentNullException.ThrowIfNull(state, nameof(state));
        ArgumentNullException.ThrowIfNull(trigger, nameof(trigger));
        ArgumentNullException.ThrowIfNull(destinationState, nameof(destinationState));

        _logger.Debug(
            "Configuring transition: {State} --({Trigger})--> {DestinationState}",
            state, trigger, destinationState);

        _stateMachine.Value.Configure(state).Permit(trigger, destinationState);
        return this;
    }

    /// <summary>
    ///     Configures a reentry transition, so firing <paramref name="trigger" /> in <paramref name="state" />
    ///     remains in that same state (<paramref name="state" />).
    /// </summary>
    /// <param name="state">The state to configure.</param>
    /// <param name="trigger">The trigger that causes reentry.</param>
    /// <returns>This <see cref="StateMachineBuilder{TState, TTrigger}" /> for fluent chaining.</returns>
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
    ///     Configures a reentry transition with an action on entry to <paramref name="state" />.
    /// </summary>
    /// <param name="state">The state to configure.</param>
    /// <param name="trigger">The trigger that causes reentry.</param>
    /// <param name="action">An action to execute upon reentry to <paramref name="state" />.</param>
    /// <returns>This <see cref="StateMachineBuilder{TState, TTrigger}" /> for fluent chaining.</returns>
    public StateMachineBuilder<TState, TTrigger> PermitReentry(TState state, TTrigger trigger, Action action)
    {
        EnsureNotBuilt();
        ArgumentNullException.ThrowIfNull(state, nameof(state));
        ArgumentNullException.ThrowIfNull(trigger, nameof(trigger));
        ArgumentNullException.ThrowIfNull(action, nameof(action));

        _logger.Debug(
            "Configuring reentry for state: {State} on trigger: {Trigger} with action.",
            state, trigger);

        _stateMachine.Value.Configure(state).PermitReentry(trigger).OnEntry(action);
        return this;
    }

    /// <summary>
    ///     Configures the machine to ignore <paramref name="trigger" /> while in <paramref name="state" />.
    /// </summary>
    /// <param name="state">The state to configure.</param>
    /// <param name="trigger">The trigger to ignore.</param>
    /// <returns>This <see cref="StateMachineBuilder{TState, TTrigger}" /> for fluent chaining.</returns>
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
    ///     Configures the machine to ignore <paramref name="trigger" /> in <paramref name="state" /> if
    ///     <paramref name="guard" />
    ///     returns <c>true</c>.
    /// </summary>
    /// <param name="state">The state to configure.</param>
    /// <param name="trigger">The trigger to ignore if <paramref name="guard" /> is true.</param>
    /// <param name="guard">A condition (delegate) to check before ignoring the trigger.</param>
    /// <param name="guardDescription">An optional description for logging or debugging the guard.</param>
    /// <returns>This <see cref="StateMachineBuilder{TState, TTrigger}" /> for fluent chaining.</returns>
    public StateMachineBuilder<TState, TTrigger> IgnoreIf(
        TState state,
        TTrigger trigger,
        Func<bool> guard,
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
    ///     Configures the machine to ignore <paramref name="trigger" /> in <paramref name="state" /> if **any** guard
    ///     in <paramref name="guards" /> returns <c>true</c>.
    /// </summary>
    /// <param name="state">The state to configure.</param>
    /// <param name="trigger">The trigger to ignore if any guard conditions are met.</param>
    /// <param name="guards">An array of (Func&lt;bool&gt;, string) tuples representing guard conditions and descriptions.</param>
    /// <returns>This <see cref="StateMachineBuilder{TState, TTrigger}" /> for fluent chaining.</returns>
    public StateMachineBuilder<TState, TTrigger> IgnoreIf(
        TState state,
        TTrigger trigger,
        params Tuple<Func<bool>, string>[] guards)
    {
        EnsureNotBuilt();
        ArgumentNullException.ThrowIfNull(state, nameof(state));
        ArgumentNullException.ThrowIfNull(trigger, nameof(trigger));
        ArgumentNullException.ThrowIfNull(guards, nameof(guards));

        _logger.Debug(
            "Configuring state: {State} to ignore trigger: {Trigger} if any guard conditions are met.",
            state, trigger);

        _stateMachine.Value.Configure(state).IgnoreIf(trigger, guards);
        return this;
    }

    /// <summary>
    ///     Configures an action to be executed upon entering <paramref name="state" />.
    /// </summary>
    /// <param name="state">The state to configure.</param>
    /// <param name="entryAction">The action to execute on entry.</param>
    /// <returns>This <see cref="StateMachineBuilder{TState, TTrigger}" /> for fluent chaining.</returns>
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
    ///     Configures an action to be executed upon entering <paramref name="state" />.
    ///     Receives the transition info and an optional action description.
    /// </summary>
    /// <param name="state">The state to configure.</param>
    /// <param name="entryAction">
    ///     A delegate taking <see cref="StateMachine{TState,TTrigger}.Transition" /> to perform upon entry.
    /// </param>
    /// <param name="entryActionDescription">An optional description of the action for logging.</param>
    /// <returns>This <see cref="StateMachineBuilder{TState, TTrigger}" /> for fluent chaining.</returns>
    public StateMachineBuilder<TState, TTrigger> OnEntry(
        TState state,
        Action<StateMachine<TState, TTrigger>.Transition> entryAction,
        string? entryActionDescription = null)
    {
        EnsureNotBuilt();
        ArgumentNullException.ThrowIfNull(state, nameof(state));
        ArgumentNullException.ThrowIfNull(entryAction, nameof(entryAction));

        _logger.Debug(
            "Configuring state: {State} to execute entry action: {EntryActionDescription}.",
            state, entryActionDescription ?? "Unnamed action");

        _stateMachine.Value.Configure(state).OnEntry(entryAction, entryActionDescription);
        return this;
    }

    /// <summary>
    ///     Configures an action to be executed upon exiting <paramref name="state" />.
    /// </summary>
    /// <param name="state">The state to configure.</param>
    /// <param name="exitAction">The action to execute on exit.</param>
    /// <returns>This <see cref="StateMachineBuilder{TState, TTrigger}" /> for fluent chaining.</returns>
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
    ///     Configures an action to be executed upon exiting <paramref name="state" />.
    ///     Receives the transition info and an optional action description.
    /// </summary>
    /// <param name="state">The state to configure.</param>
    /// <param name="exitAction">
    ///     A delegate taking <see cref="StateMachine{TState,TTrigger}.Transition" /> to perform upon exit.
    /// </param>
    /// <param name="exitActionDescription">An optional description of the action for logging.</param>
    /// <returns>This <see cref="StateMachineBuilder{TState, TTrigger}" /> for fluent chaining.</returns>
    public StateMachineBuilder<TState, TTrigger> OnExit(
        TState state,
        Action<StateMachine<TState, TTrigger>.Transition> exitAction,
        string? exitActionDescription = null)
    {
        EnsureNotBuilt();
        ArgumentNullException.ThrowIfNull(state, nameof(state));
        ArgumentNullException.ThrowIfNull(exitAction, nameof(exitAction));

        _logger.Debug(
            "Configuring state: {State} to execute exit action: {ExitActionDescription}.",
            state, exitActionDescription ?? "Unnamed action");

        _stateMachine.Value.Configure(state).OnExit(exitAction, exitActionDescription);
        return this;
    }

    /// <summary>
    ///     Declares that <paramref name="state" /> is a substate of <paramref name="superstate" />.
    ///     So transitions not handled by the substate are delegated to the superstate.
    /// </summary>
    /// <param name="state">The substate.</param>
    /// <param name="superstate">The superstate.</param>
    /// <returns>This <see cref="StateMachineBuilder{TState, TTrigger}" /> for fluent chaining.</returns>
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
    ///     Configures an internal transition within <paramref name="state" /> triggered by <paramref name="trigger" />,
    ///     executing <paramref name="transitionAction" /> if fired.
    /// </summary>
    /// <param name="state">The state to configure.</param>
    /// <param name="trigger">The trigger for the internal transition.</param>
    /// <param name="transitionAction">
    ///     The action to execute when <paramref name="trigger" /> is fired while in <paramref name="state" />.
    /// </param>
    /// <returns>This <see cref="StateMachineBuilder{TState, TTrigger}" /> for fluent chaining.</returns>
    public StateMachineBuilder<TState, TTrigger> InternalTransition(
        TState state,
        TTrigger trigger,
        Action<StateMachine<TState, TTrigger>.Transition> transitionAction)
    {
        EnsureNotBuilt();
        ArgumentNullException.ThrowIfNull(state, nameof(state));
        ArgumentNullException.ThrowIfNull(trigger, nameof(trigger));
        ArgumentNullException.ThrowIfNull(transitionAction, nameof(transitionAction));

        _logger.Debug(
            "Configuring internal transition for state: {State} on trigger: {Trigger}.",
            state, trigger);

        _stateMachine.Value.Configure(state).InternalTransition(trigger, transitionAction);
        return this;
    }

    /// <summary>
    ///     Configures a transition from <paramref name="state" /> to <paramref name="destinationState" /> triggered by
    ///     <paramref name="trigger" />, but only if <paramref name="guard" /> returns <c>true</c>.
    /// </summary>
    /// <param name="state">The source state.</param>
    /// <param name="trigger">The trigger causing the transition.</param>
    /// <param name="destinationState">The target state.</param>
    /// <param name="guard">A condition that must be met for the transition to occur.</param>
    /// <param name="guardDescription">An optional description of the guard for logging.</param>
    /// <returns>This <see cref="StateMachineBuilder{TState, TTrigger}" /> for fluent chaining.</returns>
    public StateMachineBuilder<TState, TTrigger> PermitIf(
        TState state,
        TTrigger trigger,
        TState destinationState,
        Func<bool> guard,
        string? guardDescription = null)
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
    ///     Configures a transition from <paramref name="state" /> to <paramref name="destinationState" /> triggered by
    ///     <paramref name="trigger" />, but only if **all** specified guard conditions are met.
    /// </summary>
    /// <param name="state">The source state.</param>
    /// <param name="trigger">The trigger causing the transition.</param>
    /// <param name="destinationState">The target state.</param>
    /// <param name="guards">An array of (Func&lt;bool&gt;, string) representing guard conditions.</param>
    /// <returns>This <see cref="StateMachineBuilder{TState, TTrigger}" /> for fluent chaining.</returns>
    public StateMachineBuilder<TState, TTrigger> PermitIf(
        TState state,
        TTrigger trigger,
        TState destinationState,
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
    ///     Configures an initial transition from <paramref name="state" /> to <paramref name="initialState" />.
    ///     Typically used for states that have an immediate child or substate to transition into.
    /// </summary>
    /// <param name="state">The parent (or super) state.</param>
    /// <param name="initialState">The initial substate to transition into.</param>
    /// <returns>This <see cref="StateMachineBuilder{TState, TTrigger}" /> for fluent chaining.</returns>
    public StateMachineBuilder<TState, TTrigger> InitialTransition(TState state, TState initialState)
    {
        EnsureNotBuilt();
        ArgumentNullException.ThrowIfNull(state, nameof(state));
        ArgumentNullException.ThrowIfNull(initialState, nameof(initialState));

        _logger.Debug("Configuring initial transition for state: {State} to {InitialState}.", state, initialState);

        _stateMachine.Value.Configure(state).InitialTransition(initialState);
        return this;
    }

    /// <summary>
    ///     Configures an entry action that executes specifically when entering <paramref name="state" />
    ///     from <paramref name="trigger" />.
    /// </summary>
    /// <param name="state">The state to configure.</param>
    /// <param name="trigger">The trigger that initiated the entry.</param>
    /// <param name="entryAction">
    ///     An action taking <see cref="StateMachine{TState, TTrigger}.Transition" /> describing the transition.
    /// </param>
    /// <param name="entryActionDescription">Optional description for logging the entry action.</param>
    /// <returns>This <see cref="StateMachineBuilder{TState, TTrigger}" /> for fluent chaining.</returns>
    public StateMachineBuilder<TState, TTrigger> OnEntryFrom(
        TState state,
        TTrigger trigger,
        Action<StateMachine<TState, TTrigger>.Transition> entryAction,
        string? entryActionDescription = null)
    {
        EnsureNotBuilt();
        ArgumentNullException.ThrowIfNull(state, nameof(state));
        ArgumentNullException.ThrowIfNull(trigger, nameof(trigger));
        ArgumentNullException.ThrowIfNull(entryAction, nameof(entryAction));

        _logger.Debug(
            "Configuring OnEntryFrom action for state: {State} triggered by {Trigger}.",
            state, trigger);

        _stateMachine.Value.Configure(state).OnEntryFrom(trigger, entryAction, entryActionDescription);
        return this;
    }

    /// <summary>
    ///     Finalizes configuration and returns the underlying <see cref="StateMachine{TState, TTrigger}" />
    ///     wrapped in a Result pattern for safe error handling.
    ///     No further configuration is allowed after calling this method.
    /// </summary>
    /// <returns>
    ///     A <see cref="Result{T, TError}" /> containing the configured state machine on success,
    ///     or a <see cref="BuilderError" /> if the builder has already been built.
    /// </returns>
    public Result<StateMachine<TState, TTrigger>, BuilderError> BuildSafe()
    {
        if (_isBuilt)
        {
            return Result<StateMachine<TState, TTrigger>, BuilderError>.Failure(
                BuilderError.AlreadyBuilt());
        }

        _logger.Debug("State machine build complete.");
        _isBuilt = true;
        return Result<StateMachine<TState, TTrigger>, BuilderError>.Success(_stateMachine.Value);
    }

    /// <summary>
    ///     Finalizes configuration and returns the underlying <see cref="StateMachine{TState, TTrigger}" />.
    ///     No further configuration is allowed after calling this method.
    /// </summary>
    /// <returns>The configured <see cref="StateMachine{TState, TTrigger}" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the state machine has already been built.</exception>
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
    ///     Throws an <see cref="InvalidOperationException" /> if <see cref="_isBuilt" /> is true,
    ///     preventing further modifications.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the state machine has already been built.</exception>
    private void EnsureNotBuilt()
    {
        if (_isBuilt)
        {
            throw new InvalidOperationException("Cannot configure a state machine after it has been built.");
        }
    }
}

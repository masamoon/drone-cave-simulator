namespace UnderStatic.Core
{
    public enum InteractionState
    {
        Loose,
        Held,
        Guided,
        Seated,
        Securing,
        Installed,
        Tested,
        Removing
    }

    public enum PartCategory
    {
        Motor,
        IncompatibleMotor,
        Propeller,
        Battery,
        Camera,
        Antenna,
        StrikeRack,
        Esc,
        FlightController,
        Payload
    }

    public static class InteractionStateRules
    {
        public static bool CanTransition(InteractionState from, InteractionState to)
        {
            if (from == to)
            {
                return true;
            }

            return from switch
            {
                InteractionState.Loose => to == InteractionState.Held,
                InteractionState.Held => to is InteractionState.Loose or InteractionState.Guided,
                InteractionState.Guided => to is InteractionState.Held or InteractionState.Seated,
                InteractionState.Seated => to is InteractionState.Held
                    or InteractionState.Securing
                    or InteractionState.Removing,
                InteractionState.Securing => to is InteractionState.Seated
                    or InteractionState.Installed
                    or InteractionState.Removing,
                InteractionState.Installed => to is InteractionState.Tested or InteractionState.Removing,
                InteractionState.Tested => to is InteractionState.Installed or InteractionState.Removing,
                InteractionState.Removing => to is InteractionState.Installed
                    or InteractionState.Seated
                    or InteractionState.Securing,
                _ => false
            };
        }

        public static InteractionState ResolveForPersistence(
            InteractionState current,
            InteractionState lastStable)
        {
            return current switch
            {
                InteractionState.Held or InteractionState.Guided => InteractionState.Loose,
                InteractionState.Securing => InteractionState.Seated,
                InteractionState.Removing => lastStable is InteractionState.Tested
                    ? InteractionState.Tested
                    : InteractionState.Installed,
                _ => current
            };
        }

        public static bool IsStable(InteractionState state)
        {
            return state is InteractionState.Loose
                or InteractionState.Seated
                or InteractionState.Installed
                or InteractionState.Tested;
        }
    }
}

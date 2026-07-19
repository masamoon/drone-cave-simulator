using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace UnderStatic.Tests.PlayMode
{
    [SetUpFixture]
    public sealed class PlayModeInputFixture
    {
        [OneTimeSetUp]
        public void EnsureBatchModeKeyboard()
        {
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;

            if (Keyboard.current != null && Mouse.current != null)
            {
                return;
            }

            var ignored = LogAssert.ignoreFailingMessages;
            try
            {
                // PlayerInput selects the Keyboard&Mouse control scheme as a pair.
                // Batch-mode players expose neither device, so provide both before
                // the SafeHouse factory creates its PlayerInput component.
                LogAssert.ignoreFailingMessages = true;
                if (Keyboard.current == null)
                {
                    InputSystem.AddDevice<Keyboard>();
                }

                if (Mouse.current == null)
                {
                    InputSystem.AddDevice<Mouse>();
                }
            }
            finally
            {
                LogAssert.ignoreFailingMessages = ignored;
            }
        }
    }
}

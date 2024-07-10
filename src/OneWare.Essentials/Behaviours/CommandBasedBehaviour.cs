using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;

namespace OneWare.Essentials.Behaviours
{
    public class CommandBasedBehaviour : Behavior<Control>
    {
        /// <summary>
        ///     Defines the <see cref="Command" /> property.
        /// </summary>
        public static readonly DirectProperty<CommandBasedBehaviour, ICommand?> CommandProperty =
            AvaloniaProperty.RegisterDirect<CommandBasedBehaviour, ICommand?>(nameof(Command),
                commandBehavior => commandBehavior.Command,
                (commandBehavior, command) => commandBehavior.Command = command, enableDataValidation: true);

        /// <summary>
        ///     Defines the <see cref="CommandParameter" /> property.
        /// </summary>
        public static readonly StyledProperty<object?> CommandParameterProperty =
            AvaloniaProperty.Register<CommandBasedBehaviour, object?>(nameof(CommandParameter));

        private ICommand? _command;

        /// <summary>
        ///     Gets or sets an <see cref="ICommand" /> to be invoked when the button is clicked.
        /// </summary>
        public ICommand? Command
        {
            get => _command;
            set => SetAndRaise(CommandProperty, ref _command, value);
        }

        /// <summary>
        ///     Gets or sets a parameter to be passed to the <see cref="Command" />.
        /// </summary>
        public object? CommandParameter
        {
            get => GetValue(CommandParameterProperty);
            set => SetValue(CommandParameterProperty, value);
        }

        protected bool ExecuteCommand()
        {
            if (Command != null && Command.CanExecute(CommandParameter))
            {
                Command.Execute(CommandParameter);
                return true;
            }

            return false;
        }
    }
}
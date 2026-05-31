using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DapperClassMate.UI.Controls
{
    public partial class IntegerSpinEdit : UserControl
    {
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(
                nameof(Value),
                typeof(int),
                typeof(IntegerSpinEdit),
                new FrameworkPropertyMetadata(
                    0,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnValueChanged));

        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(
                nameof(Minimum),
                typeof(int),
                typeof(IntegerSpinEdit),
                new PropertyMetadata(0));

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(
                nameof(Maximum),
                typeof(int),
                typeof(IntegerSpinEdit),
                new PropertyMetadata(int.MaxValue));

        public static readonly DependencyProperty IncrementProperty =
            DependencyProperty.Register(
                nameof(Increment),
                typeof(int),
                typeof(IntegerSpinEdit),
                new PropertyMetadata(1));

        public int Value
        {
            get { return (int)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, Clamp(value)); }
        }

        public int Minimum
        {
            get { return (int)GetValue(MinimumProperty); }
            set { SetValue(MinimumProperty, value); }
        }

        public int Maximum
        {
            get { return (int)GetValue(MaximumProperty); }
            set { SetValue(MaximumProperty, value); }
        }

        public int Increment
        {
            get { return (int)GetValue(IncrementProperty); }
            set { SetValue(IncrementProperty, value); }
        }

        private bool _suppressTextChange;

        public IntegerSpinEdit()
        {
            InitializeComponent();
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var spinEdit = (IntegerSpinEdit)d;
            spinEdit.SyncTextFromValue();
        }

        private void SyncTextFromValue()
        {
            _suppressTextChange = true;
            ValueTextBox.Text = Value.ToString();
            _suppressTextChange = false;
        }

        private int Clamp(int value)
        {
            if (value < Minimum) return Minimum;
            if (value > Maximum) return Maximum;
            return value;
        }

        private void UpButton_Click(object sender, RoutedEventArgs e)
        {
            Value = Clamp(Value + Increment);
        }

        private void DownButton_Click(object sender, RoutedEventArgs e)
        {
            Value = Clamp(Value - Increment);
        }

        private void ValueTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressTextChange)
                return;

            if (int.TryParse(ValueTextBox.Text, out int parsed))
            {
                _suppressTextChange = true;
                Value = Clamp(parsed);
                _suppressTextChange = false;
            }
        }

        private void ValueTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // Revert any non-numeric or out-of-range text to the current value.
            SyncTextFromValue();
        }

        private void ValueTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Up)
            {
                Value = Clamp(Value + Increment);
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                Value = Clamp(Value - Increment);
                e.Handled = true;
            }
        }
    }
}
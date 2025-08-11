using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SharpShot.UI
{
    public partial class ColorWheelControl : UserControl
    {
        public event EventHandler<string>? ColorChanged;
        
        private bool _isUpdating = false;

        public ColorWheelControl()
        {
            InitializeComponent();
            Loaded += ColorWheelControl_Loaded;
        }

        private void ColorWheelControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Set initial color to theme color
            try
            {
                var accentBrush = Application.Current.Resources["AccentBrush"] as SolidColorBrush;
                if (accentBrush != null)
                {
                    var color = accentBrush.Color;
                    RedSlider.Value = color.R;
                    GreenSlider.Value = color.G;
                    BlueSlider.Value = color.B;
                }
            }
            catch
            {
                // Fallback to default orange if theme color not available
                RedSlider.Value = 255;
                GreenSlider.Value = 140;
                BlueSlider.Value = 0;
            }
            
            UpdateColorDisplay();
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isUpdating)
            {
                UpdateColorDisplay();
            }
        }

        private void UpdateColorDisplay()
        {
            try
            {
                var red = (byte)RedSlider.Value;
                var green = (byte)GreenSlider.Value;
                var blue = (byte)BlueSlider.Value;

                // Update value displays
                RedValue.Text = red.ToString();
                GreenValue.Text = green.ToString();
                BlueValue.Text = blue.ToString();

                // Create color
                var color = Color.FromRgb(red, green, blue);

                // Update preview
                ColorPreview.Background = new SolidColorBrush(color);

                // Update hex display
                HexColorText.Text = $"#{red:X2}{green:X2}{blue:X2}";

                // Raise event
                ColorChanged?.Invoke(this, HexColorText.Text);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating color display: {ex.Message}");
            }
        }

        public void SetColor(string hexColor)
        {
            try
            {
                _isUpdating = true;

                var color = (Color)ColorConverter.ConvertFromString(hexColor);
                
                // Update sliders
                RedSlider.Value = color.R;
                GreenSlider.Value = color.G;
                BlueSlider.Value = color.B;

                // Update display
                UpdateColorDisplay();
            }
            catch
            {
                // If parsing fails, try to use theme color
                try
                {
                    var accentBrush = Application.Current.Resources["AccentBrush"] as SolidColorBrush;
                    if (accentBrush != null)
                    {
                        var themeColor = accentBrush.Color;
                        RedSlider.Value = themeColor.R;
                        GreenSlider.Value = themeColor.G;
                        BlueSlider.Value = themeColor.B;
                    }
                    else
                    {
                        // Fallback to default orange color
                        RedSlider.Value = 255;
                        GreenSlider.Value = 140;
                        BlueSlider.Value = 0;
                    }
                }
                catch
                {
                    // Ultimate fallback to default orange color
                    RedSlider.Value = 255;
                    GreenSlider.Value = 140;
                    BlueSlider.Value = 0;
                }
                UpdateColorDisplay();
            }
            finally
            {
                _isUpdating = false;
            }
        }
    }
} 
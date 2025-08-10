using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using SharpShot.Services;
using Point = System.Windows.Point;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace SharpShot.UI
{
    public partial class ScreenshotEditorWindow : Window
    {
        private readonly Bitmap _originalBitmap;
        private readonly ScreenshotService _screenshotService;
        private readonly SettingsService? _settingsService;
        
        // Current editing state
        private EditingTool _currentTool = EditingTool.None;
        private System.Windows.Media.Color _currentColor = System.Windows.Media.Color.FromRgb(255, 140, 0); // Theme orange
        private double _currentStrokeWidth = 2.0;
        private double _currentBlurStrength = 8.0;
        private double _currentHighlighterOpacity = 0.5;
        private double _currentFontSize = 16.0; // New field for font size
        
        // Drawing state
        private bool _isDrawing = false;
        private Point _startPoint;
        private Point _endPoint;
        private UIElement? _currentDrawingElement;
        
        // Pen drawing state
        private Polyline? _currentPolyline;
        private readonly List<Point> _currentStroke = new();
        
        // Undo/Redo stacks
        private readonly Stack<List<UIElement>> _undoStack = new();
        private readonly Stack<List<UIElement>> _redoStack = new();
        
        public Bitmap? FinalBitmap { get; private set; }
        public bool ImageSaved { get; private set; } = false;
        public bool ImageCopied { get; private set; } = false;

        public enum EditingTool
        {
            None,
            Blur,
            Arrow,
            Rectangle,
            Circle,
            Line,
            Pen,
            Text,
            Highlight
        }

        public ScreenshotEditorWindow(Bitmap bitmap, ScreenshotService screenshotService, SettingsService? settingsService = null)
        {
            InitializeComponent();
            _originalBitmap = bitmap;
            _screenshotService = screenshotService;
            _settingsService = settingsService;
            
            InitializeEditor();
        }

        /// <summary>
        /// Public method to refresh the theme when settings change
        /// </summary>
        public void RefreshTheme()
        {
            ApplyThemeSettings();
        }

        private void InitializeEditor()
        {
            // Convert bitmap to BitmapSource and display
            var bitmapSource = ConvertBitmapToBitmapSource(_originalBitmap);
            ScreenshotImage.Source = bitmapSource;
            
            // Center the image on screen
            CenterImageOnScreen();
            
            // Set up event handlers
            OverlayCanvas.MouseLeftButtonDown += OverlayCanvas_MouseLeftButtonDown;
            OverlayCanvas.MouseLeftButtonUp += OverlayCanvas_MouseLeftButtonUp;
            OverlayCanvas.MouseMove += OverlayCanvas_MouseMove;
            
            // Initialize with current theme color
            if (_settingsService?.CurrentSettings?.IconColor != null)
            {
                var themeColorStr = _settingsService.CurrentSettings.IconColor;
                if (System.Windows.Media.ColorConverter.ConvertFromString(themeColorStr) is System.Windows.Media.Color themeColor)
                {
                    _currentColor = themeColor;
                }
            }
            
            // Apply theme settings to all overlay edit menu icons
            ApplyThemeSettings();
            
            // Take initial snapshot for undo
            SaveStateForUndo();
        }

        private void ApplyThemeSettings()
        {
            try
            {
                if (_settingsService?.CurrentSettings?.IconColor == null) return;
                
                var themeColor = _settingsService.CurrentSettings.IconColor;
                if (string.IsNullOrEmpty(themeColor)) return;
                
                var brush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(themeColor));
                
                // Update the global AccentBrush resource so all XAML elements using {DynamicResource AccentBrush} update automatically
                Application.Current.Resources["AccentBrush"] = brush;
                
                // Update all overlay edit menu icons to use the theme color
                UpdateIconColor(BlurButton, brush);
                UpdateIconColor(ArrowButton, brush);
                UpdateIconColor(RectangleButton, brush);
                UpdateIconColor(CircleButton, brush);
                UpdateIconColor(LineButton, brush);
                UpdateIconColor(PenButton, brush);
                UpdateIconColor(TextButton, brush);
                UpdateIconColor(HighlightButton, brush);
                UpdateIconColor(UndoButton, brush);
                UpdateIconColor(RedoButton, brush);
                UpdateIconColor(CopyFinalButton, brush);
                UpdateIconColor(SaveFinalButton, brush);
                UpdateIconColor(CloseEditorButton, brush);
                
                // Update separator colors
                UpdateSeparatorColors(brush);
                
                // Update button style colors for hover/pressed states
                UpdateButtonStyleColors(brush);
                
                // Update the current color for new drawings
                _currentColor = brush.Color;
                
                // Refresh tool button states to use new theme color
                ResetToolButtonStates();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to apply theme settings: {ex.Message}");
            }
        }
        
        private void UpdateIconColor(Button button, SolidColorBrush brush)
        {
            try
            {
                if (button.Content is System.Windows.Shapes.Path path)
                {
                    path.Stroke = brush;
                    path.Fill = brush;
                }
                else if (button.Content is Ellipse ellipse)
                {
                    ellipse.Fill = brush;
                    ellipse.Stroke = brush;
                }
                else if (button.Content is Rectangle rect)
                {
                    rect.Stroke = brush;
                }
                else if (button.Content is Line line)
                {
                    line.Stroke = brush;
                }
                else if (button.Content is TextBlock textBlock)
                {
                    textBlock.Foreground = brush;
                }
                else if (button.Content is StackPanel stackPanel)
                {
                    // Handle buttons with text and icons (Copy, Save)
                    foreach (var child in stackPanel.Children)
                    {
                        if (child is System.Windows.Shapes.Path pathChild)
                        {
                            pathChild.Stroke = brush;
                            pathChild.Fill = brush;
                        }
                        else if (child is TextBlock textChild)
                        {
                            textChild.Foreground = brush;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update icon color for button {button.Name}: {ex.Message}");
            }
        }
        
        private void UpdateSeparatorColors(SolidColorBrush brush)
        {
            try
            {
                // Find all separator rectangles in the toolbar
                var toolbar = CloseEditorButton.Parent as StackPanel;
                if (toolbar != null)
                {
                    foreach (var child in toolbar.Children)
                    {
                        if (child is Rectangle rect && rect.Name == "")
                        {
                            rect.Fill = brush;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update separator colors: {ex.Message}");
            }
                }
        
        private void UpdateButtonStyleColors(SolidColorBrush brush)
        {
            try
            {
                // Apply the same theme-aware styling pattern used in MainWindow
                // Create dynamic styles for all the editor buttons
                var themeColor = brush.Color;
                var hoverBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(32, themeColor.R, themeColor.G, themeColor.B));
                
                // Apply theme-aware styling to all editor buttons
                UndoButton.Style = CreateThemeAwareButtonStyle(themeColor, hoverBrush);
                RedoButton.Style = CreateThemeAwareButtonStyle(themeColor, hoverBrush);
                CopyFinalButton.Style = CreateThemeAwareButtonStyle(themeColor, hoverBrush);
                SaveFinalButton.Style = CreateThemeAwareButtonStyle(themeColor, hoverBrush);
                CloseEditorButton.Style = CreateThemeAwareButtonStyle(themeColor, hoverBrush);
                
                // Also apply to tool buttons
                BlurButton.Style = CreateThemeAwareButtonStyle(themeColor, hoverBrush);
                ArrowButton.Style = CreateThemeAwareButtonStyle(themeColor, hoverBrush);
                RectangleButton.Style = CreateThemeAwareButtonStyle(themeColor, hoverBrush);
                CircleButton.Style = CreateThemeAwareButtonStyle(themeColor, hoverBrush);
                LineButton.Style = CreateThemeAwareButtonStyle(themeColor, hoverBrush);
                PenButton.Style = CreateThemeAwareButtonStyle(themeColor, hoverBrush);
                TextButton.Style = CreateThemeAwareButtonStyle(themeColor, hoverBrush);
                HighlightButton.Style = CreateThemeAwareButtonStyle(themeColor, hoverBrush);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update button style colors: {ex.Message}");
            }
        }
        
        private void CenterImageOnScreen()
        {
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            
            var imageWidth = _originalBitmap.Width;
            var imageHeight = _originalBitmap.Height;
            
            // Center the image on screen
            Canvas.SetLeft(ScreenshotImage, (screenWidth - imageWidth) / 2);
            Canvas.SetTop(ScreenshotImage, (screenHeight - imageHeight) / 2);
            
            // Set overlay canvas to same position and size
            Canvas.SetLeft(OverlayCanvas, (screenWidth - imageWidth) / 2);
            Canvas.SetTop(OverlayCanvas, (screenHeight - imageHeight) / 2);
            OverlayCanvas.Width = imageWidth;
            OverlayCanvas.Height = imageHeight;
        }

        private BitmapSource ConvertBitmapToBitmapSource(Bitmap bitmap)
        {
            using var memory = new MemoryStream();
            bitmap.Save(memory, ImageFormat.Png);
            memory.Position = 0;
            
            var bitmapDecoder = BitmapDecoder.Create(memory, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            return bitmapDecoder.Frames[0];
        }

        #region Tool Selection Events

        private void BlurButton_Click(object sender, RoutedEventArgs e)
        {
            SetCurrentTool(EditingTool.Blur);
            ShowColorPicker();
        }

        private void ArrowButton_Click(object sender, RoutedEventArgs e)
        {
            SetCurrentTool(EditingTool.Arrow);
            ShowColorPicker();
        }

        private void RectangleButton_Click(object sender, RoutedEventArgs e)
        {
            SetCurrentTool(EditingTool.Rectangle);
            ShowColorPicker();
        }

        private void CircleButton_Click(object sender, RoutedEventArgs e)
        {
            SetCurrentTool(EditingTool.Circle);
            ShowColorPicker();
        }

        private void LineButton_Click(object sender, RoutedEventArgs e)
        {
            SetCurrentTool(EditingTool.Line);
            ShowColorPicker();
        }

        private void PenButton_Click(object sender, RoutedEventArgs e)
        {
            SetCurrentTool(EditingTool.Pen);
            ShowColorPicker();
        }

        private void TextButton_Click(object sender, RoutedEventArgs e)
        {
            SetCurrentTool(EditingTool.Text);
            ShowColorPicker();
        }

        private void HighlightButton_Click(object sender, RoutedEventArgs e)
        {
            SetCurrentTool(EditingTool.Highlight);
            ShowColorPicker();
        }

        private void SetCurrentTool(EditingTool tool)
        {
            _currentTool = tool;
            
            // Update button visual states (simplified - in real implementation would use proper MVVM)
            ResetToolButtonStates();
            
            // Set cursor based on tool
            OverlayCanvas.Cursor = tool switch
            {
                EditingTool.Blur => Cursors.Cross,
                EditingTool.Pen => Cursors.Pen,
                EditingTool.Text => Cursors.IBeam,
                _ => Cursors.Cross
            };

            // Show/hide tool-specific panels
            BlurStrengthPanel.Visibility = _currentTool == EditingTool.Blur ? Visibility.Visible : Visibility.Collapsed;
            HighlighterOpacityPanel.Visibility = _currentTool == EditingTool.Highlight ? Visibility.Visible : Visibility.Collapsed;
            FontSizePanel.Visibility = _currentTool == EditingTool.Text ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ResetToolButtonStates()
        {
            // Reset all tool button backgrounds (simplified approach)
            BlurButton.Background = System.Windows.Media.Brushes.Transparent;
            ArrowButton.Background = System.Windows.Media.Brushes.Transparent;
            RectangleButton.Background = System.Windows.Media.Brushes.Transparent;
            CircleButton.Background = System.Windows.Media.Brushes.Transparent;
            LineButton.Background = System.Windows.Media.Brushes.Transparent;
            PenButton.Background = System.Windows.Media.Brushes.Transparent;
            TextButton.Background = System.Windows.Media.Brushes.Transparent;
            HighlightButton.Background = System.Windows.Media.Brushes.Transparent;
            
            // Highlight current tool using theme color
            var selectedBrush = System.Windows.Media.Brushes.Transparent;
            if (_settingsService?.CurrentSettings?.IconColor != null)
            {
                var themeColorStr = _settingsService.CurrentSettings.IconColor;
                if (System.Windows.Media.ColorConverter.ConvertFromString(themeColorStr) is System.Windows.Media.Color themeColor)
                {
                    selectedBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(64, themeColor.R, themeColor.G, themeColor.B));
                }
            }
            else
            {
                // Fallback to default orange if no theme color
                selectedBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(64, 255, 140, 0));
            }
            
            switch (_currentTool)
            {
                case EditingTool.Blur: BlurButton.Background = selectedBrush; break;
                case EditingTool.Arrow: ArrowButton.Background = selectedBrush; break;
                case EditingTool.Rectangle: RectangleButton.Background = selectedBrush; break;
                case EditingTool.Circle: CircleButton.Background = selectedBrush; break;
                case EditingTool.Line: LineButton.Background = selectedBrush; break;
                case EditingTool.Pen: PenButton.Background = selectedBrush; break;
                case EditingTool.Text: TextButton.Background = selectedBrush; break;
                case EditingTool.Highlight: HighlightButton.Background = selectedBrush; break;
            }
        }

        private void ShowColorPicker()
        {
            ColorPickerPanel.Visibility = Visibility.Visible;
            
            // Show/hide tool-specific panels
            BlurStrengthPanel.Visibility = _currentTool == EditingTool.Blur ? Visibility.Visible : Visibility.Collapsed;
            HighlighterOpacityPanel.Visibility = _currentTool == EditingTool.Highlight ? Visibility.Visible : Visibility.Collapsed;
            FontSizePanel.Visibility = _currentTool == EditingTool.Text ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

        #region Drawing Events

        private void OverlayCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_currentTool == EditingTool.None) return;
            
            _isDrawing = true;
            _startPoint = e.GetPosition(OverlayCanvas);
            
            if (_currentTool == EditingTool.Text)
            {
                AddTextAtPoint(_startPoint);
                return;
            }
            
            if (_currentTool == EditingTool.Pen)
            {
                StartPenDrawing(_startPoint);
                return;
            }
            
            OverlayCanvas.CaptureMouse();
        }

        private void OverlayCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDrawing || _currentTool == EditingTool.None || _currentTool == EditingTool.Text) return;
            
            _endPoint = e.GetPosition(OverlayCanvas);
            
            if (_currentTool == EditingTool.Pen)
            {
                ContinuePenDrawing(_endPoint);
                return;
            }
            
            // Remove previous preview element
            if (_currentDrawingElement != null)
            {
                OverlayCanvas.Children.Remove(_currentDrawingElement);
            }
            
            // Create preview element
            _currentDrawingElement = CreateDrawingElement(_startPoint, _endPoint);
            if (_currentDrawingElement != null)
            {
                OverlayCanvas.Children.Add(_currentDrawingElement);
            }
        }

        private void OverlayCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDrawing || _currentTool == EditingTool.None) return;
            
            _isDrawing = false;
            _endPoint = e.GetPosition(OverlayCanvas);
            
            if (_currentTool == EditingTool.Pen)
            {
                FinishPenDrawing();
                return;
            }
            
            // Finalize the drawing element
            if (_currentDrawingElement != null)
            {
                // Element is already added, just finalize it
                SaveStateForUndo();
            }
            
            _currentDrawingElement = null;
            OverlayCanvas.ReleaseMouseCapture();
        }

        private UIElement? CreateDrawingElement(Point start, Point end)
        {
            var brush = new SolidColorBrush(_currentColor);
            
            return _currentTool switch
            {
                EditingTool.Rectangle => CreateRectangleElement(start, end, brush),
                EditingTool.Circle => CreateCircleElement(start, end, brush),
                EditingTool.Line => CreateLineElement(start, end, brush),
                EditingTool.Arrow => CreateArrowElement(start, end, brush),
                EditingTool.Highlight => CreateHighlightElement(start, end),
                EditingTool.Blur => CreateBlurElement(start, end),
                _ => null
            };
        }

        private Rectangle CreateRectangleElement(Point start, Point end, SolidColorBrush brush)
        {
            var rect = new Rectangle
            {
                Stroke = brush,
                StrokeThickness = _currentStrokeWidth,
                Fill = System.Windows.Media.Brushes.Transparent,
                Width = Math.Abs(end.X - start.X),
                Height = Math.Abs(end.Y - start.Y)
            };
            
            Canvas.SetLeft(rect, Math.Min(start.X, end.X));
            Canvas.SetTop(rect, Math.Min(start.Y, end.Y));
            
            return rect;
        }

        private System.Windows.Shapes.Ellipse CreateCircleElement(Point start, Point end, SolidColorBrush brush)
        {
            var ellipse = new System.Windows.Shapes.Ellipse
            {
                Stroke = brush,
                StrokeThickness = _currentStrokeWidth,
                Fill = System.Windows.Media.Brushes.Transparent,
                Width = Math.Abs(end.X - start.X),
                Height = Math.Abs(end.Y - start.Y)
            };
            
            Canvas.SetLeft(ellipse, Math.Min(start.X, end.X));
            Canvas.SetTop(ellipse, Math.Min(start.Y, end.Y));
            
            return ellipse;
        }

        private Line CreateLineElement(Point start, Point end, SolidColorBrush brush)
        {
            var line = new Line
            {
                X1 = start.X,
                Y1 = start.Y,
                X2 = end.X,
                Y2 = end.Y,
                Stroke = brush,
                StrokeThickness = _currentStrokeWidth,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            
            return line;
        }

        private Canvas CreateArrowElement(Point start, Point end, SolidColorBrush brush)
        {
            var canvas = new Canvas();
            
            // Main line
            var mainLine = new Line
            {
                X1 = start.X,
                Y1 = start.Y,
                X2 = end.X,
                Y2 = end.Y,
                Stroke = brush,
                StrokeThickness = _currentStrokeWidth,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            
            // Calculate arrow head lines
            var angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
            var arrowLength = Math.Max(8, _currentStrokeWidth * 4); // Proportional to stroke width
            var arrowAngle = Math.PI / 6; // 30 degrees
            
            var arrowPoint1 = new Point(
                end.X - arrowLength * Math.Cos(angle - arrowAngle),
                end.Y - arrowLength * Math.Sin(angle - arrowAngle)
            );
            
            var arrowPoint2 = new Point(
                end.X - arrowLength * Math.Cos(angle + arrowAngle),
                end.Y - arrowLength * Math.Sin(angle + arrowAngle)
            );
            
            // Arrow head lines (same thickness as main line)
            var arrowLine1 = new Line
            {
                X1 = end.X,
                Y1 = end.Y,
                X2 = arrowPoint1.X,
                Y2 = arrowPoint1.Y,
                Stroke = brush,
                StrokeThickness = _currentStrokeWidth,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            
            var arrowLine2 = new Line
            {
                X1 = end.X,
                Y1 = end.Y,
                X2 = arrowPoint2.X,
                Y2 = arrowPoint2.Y,
                Stroke = brush,
                StrokeThickness = _currentStrokeWidth,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            
            // Add all lines to canvas
            canvas.Children.Add(mainLine);
            canvas.Children.Add(arrowLine1);
            canvas.Children.Add(arrowLine2);
            
            return canvas;
        }

        private Rectangle CreateHighlightElement(Point start, Point end)
        {
            var alpha = (byte)(_currentHighlighterOpacity * 255);
            var highlightBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, _currentColor.R, _currentColor.G, _currentColor.B));
            
            var rect = new Rectangle
            {
                Fill = highlightBrush,
                Width = Math.Abs(end.X - start.X),
                Height = Math.Max(Math.Abs(end.Y - start.Y), 20) // Minimum height for highlight
            };
            
            Canvas.SetLeft(rect, Math.Min(start.X, end.X));
            Canvas.SetTop(rect, Math.Min(start.Y, end.Y));
            
            return rect;
        }

        private Border CreateBlurElement(Point start, Point end)
        {
            var width = Math.Abs(end.X - start.X);
            var height = Math.Abs(end.Y - start.Y);
            
            // Create a border with blur effect
            var blurBorder = new Border
            {
                Width = width,
                Height = height,
                Background = CreateSampledBackgroundBrush(start, end),
                ClipToBounds = true
            };
            
            // Apply Gaussian blur effect
            var blurEffect = new BlurEffect
            {
                Radius = _currentBlurStrength,
                KernelType = KernelType.Gaussian
            };
            
            blurBorder.Effect = blurEffect;
            
            Canvas.SetLeft(blurBorder, Math.Min(start.X, end.X));
            Canvas.SetTop(blurBorder, Math.Min(start.Y, end.Y));
            
            return blurBorder;
        }

        private System.Windows.Media.Brush CreateSampledBackgroundBrush(Point start, Point end)
        {
            try
            {
                // Sample colors from the original bitmap at the blur area
                var sampledColors = SampleColorsFromBitmap(start, end);
                
                var gradientBrush = new LinearGradientBrush();
                
                for (int i = 0; i < sampledColors.Count; i++)
                {
                    var stop = new GradientStop(sampledColors[i], i / (double)(sampledColors.Count - 1));
                    gradientBrush.GradientStops.Add(stop);
                }
                
                // Randomize gradient direction for blur effect
                var random = new Random();
                gradientBrush.StartPoint = new Point(random.NextDouble(), random.NextDouble());
                gradientBrush.EndPoint = new Point(random.NextDouble(), random.NextDouble());
                
                return gradientBrush;
            }
            catch
            {
                // Fallback to default colors if sampling fails
                return new SolidColorBrush(System.Windows.Media.Color.FromRgb(128, 128, 128));
            }
        }

        private List<System.Windows.Media.Color> SampleColorsFromBitmap(Point start, Point end)
        {
            var colors = new List<System.Windows.Media.Color>();
            var random = new Random();
            
            // Get the area to sample from
            var left = Math.Max(0, (int)Math.Min(start.X, end.X));
            var top = Math.Max(0, (int)Math.Min(start.Y, end.Y));
            var right = Math.Min(_originalBitmap.Width - 1, (int)Math.Max(start.X, end.X));
            var bottom = Math.Min(_originalBitmap.Height - 1, (int)Math.Max(start.Y, end.Y));
            
            // Sample 4-6 random colors from the area
            for (int i = 0; i < 5; i++)
            {
                var x = random.Next(left, right + 1);
                var y = random.Next(top, bottom + 1);
                
                try
                {
                    var pixel = _originalBitmap.GetPixel(x, y);
                    var color = System.Windows.Media.Color.FromRgb(pixel.R, pixel.G, pixel.B);
                    colors.Add(color);
                }
                catch
                {
                    // Add fallback color if sampling fails
                    colors.Add(System.Windows.Media.Color.FromRgb(128, 128, 128));
                }
            }
            
            // Ensure we have at least some colors
            if (colors.Count == 0)
            {
                colors.Add(System.Windows.Media.Color.FromRgb(128, 128, 128));
                colors.Add(System.Windows.Media.Color.FromRgb(140, 140, 140));
            }
            
            return colors;
        }

        private void StartPenDrawing(Point startPoint)
        {
            _currentStroke.Clear();
            _currentStroke.Add(startPoint);
            
            _currentPolyline = new Polyline
            {
                Stroke = new SolidColorBrush(_currentColor),
                StrokeThickness = _currentStrokeWidth,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            
            _currentPolyline.Points.Add(startPoint);
            OverlayCanvas.Children.Add(_currentPolyline);
            OverlayCanvas.CaptureMouse();
        }

        private void ContinuePenDrawing(Point currentPoint)
        {
            if (_currentPolyline != null)
            {
                _currentStroke.Add(currentPoint);
                _currentPolyline.Points.Add(currentPoint);
            }
        }

        private void FinishPenDrawing()
        {
            if (_currentPolyline != null)
            {
                SaveStateForUndo();
                _currentPolyline = null;
                _currentStroke.Clear();
            }
            
            OverlayCanvas.ReleaseMouseCapture();
        }

        private void AddTextAtPoint(Point point)
        {
            var textBox = new TextBox
            {
                Background = System.Windows.Media.Brushes.Transparent,
                BorderBrush = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(_currentColor),
                FontSize = _currentFontSize,
                MinWidth = 100,
                Text = "Text"
            };
            
            Canvas.SetLeft(textBox, point.X);
            Canvas.SetTop(textBox, point.Y);
            
            OverlayCanvas.Children.Add(textBox);
            textBox.Focus();
            textBox.SelectAll();
            
            SaveStateForUndo();
        }

        #endregion

        #region Color and Style Events

        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string colorString)
            {
                if (colorString == "Transparent")
                {
                    _currentColor = Colors.Transparent;
                }
                else if (System.Windows.Media.ColorConverter.ConvertFromString(colorString) is System.Windows.Media.Color color)
                {
                    _currentColor = color;
                }
                
                // Update current color display
                CurrentColorDisplay.Fill = new SolidColorBrush(_currentColor);
            }
        }

        private void StrokeWidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _currentStrokeWidth = e.NewValue;
            if (StrokeWidthValue != null)
            {
                StrokeWidthValue.Text = $"{(int)e.NewValue}px";
            }
        }

        private void BlurStrengthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _currentBlurStrength = e.NewValue;
            if (BlurStrengthValue != null)
            {
                BlurStrengthValue.Text = $"{(int)e.NewValue}";
            }
        }

        private void HighlighterOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (HighlighterOpacityValue != null)
            {
                _currentHighlighterOpacity = e.NewValue;
                var percentage = (int)(e.NewValue * 100);
                HighlighterOpacityValue.Text = $"{percentage}%";
            }
        }

        private void FontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (FontSizeValue != null)
            {
                _currentFontSize = e.NewValue;
                FontSizeValue.Text = $"{e.NewValue:F0}px";
            }
        }

        #endregion

        #region Undo/Redo

        private void SaveStateForUndo()
        {
            var currentState = new List<UIElement>();
            foreach (UIElement element in OverlayCanvas.Children)
            {
                currentState.Add(element);
            }
            _undoStack.Push(currentState);
            _redoStack.Clear(); // Clear redo stack when new action is performed
        }

        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            if (_undoStack.Count > 1) // Keep at least the initial state
            {
                var currentState = _undoStack.Pop();
                _redoStack.Push(currentState);
                
                var previousState = _undoStack.Peek();
                RestoreState(previousState);
            }
        }

        private void RedoButton_Click(object sender, RoutedEventArgs e)
        {
            if (_redoStack.Count > 0)
            {
                var redoState = _redoStack.Pop();
                _undoStack.Push(redoState);
                RestoreState(redoState);
            }
        }

        private void RestoreState(List<UIElement> state)
        {
            OverlayCanvas.Children.Clear();
            foreach (var element in state)
            {
                OverlayCanvas.Children.Add(element);
            }
        }

        #endregion

        #region Final Actions

        private void CopyFinalButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var finalBitmap = RenderToBitmap();
                FinalBitmap = finalBitmap;
                
                // Copy to clipboard
                using var stream = new MemoryStream();
                finalBitmap.Save(stream, ImageFormat.Png);
                stream.Position = 0;
                
                var bitmapSource = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                Clipboard.SetImage(bitmapSource);
                
                ImageCopied = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveFinalButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var finalBitmap = RenderToBitmap();
                FinalBitmap = finalBitmap;
                
                var filePath = _screenshotService.SaveScreenshot(finalBitmap);
                ImageSaved = true;
                
                MessageBox.Show($"Screenshot saved to: {filePath}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Bitmap RenderToBitmap()
        {
            try
            {
                // Get the current window position and size
                var windowLeft = (int)Left;
                var windowTop = (int)Top;
                var windowWidth = (int)Width;
                var windowHeight = (int)Height;
                
                // Calculate the actual screenshot area (accounting for window borders and title bar)
                // The image is displayed in the ImageControl, so we need to find its screen coordinates
                var imageControl = ScreenshotImage;
                var imagePoint = imageControl.PointToScreen(new Point(0, 0));
                
                var screenshotX = (int)imagePoint.X;
                var screenshotY = (int)imagePoint.Y;
                var screenshotWidth = (int)imageControl.ActualWidth;
                var screenshotHeight = (int)imageControl.ActualHeight;
                
                // Take a new screenshot of the same area
                using var newBitmap = new Bitmap(screenshotWidth, screenshotHeight);
                using var graphics = Graphics.FromImage(newBitmap);
                
                // Copy from screen at the calculated coordinates
                graphics.CopyFromScreen(screenshotX, screenshotY, 0, 0, new System.Drawing.Size(screenshotWidth, screenshotHeight));
                
                // Return a copy of the bitmap
                return new Bitmap(newBitmap);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to capture new screenshot: {ex.Message}");
                // Fallback to original bitmap if screenshot fails
                return new Bitmap(_originalBitmap);
            }
        }

        private void CloseEditorButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private Style CreateThemeAwareButtonStyle(System.Windows.Media.Color themeColor, System.Windows.Media.Brush hoverBrush)
        {
            // Get the base EditToolButtonStyle and create a new style based on it
            var baseStyle = this.Resources["EditToolButtonStyle"] as Style;
            var style = new Style(typeof(Button), baseStyle);
            
            // Override the hover effect to use theme color
            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(BackgroundProperty, hoverBrush));
            hoverTrigger.Setters.Add(new Setter(EffectProperty, new DropShadowEffect
            {
                Color = themeColor,
                BlurRadius = 10,
                ShadowDepth = 0,
                Opacity = 0.15
            }));
            
            style.Triggers.Add(hoverTrigger);
            
            return style;
        }

        #endregion

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            
            if (e.Key == Key.Escape)
            {
                Close();
            }
            else if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
            {
                UndoButton_Click(this, new RoutedEventArgs());
            }
            else if (e.Key == Key.Y && Keyboard.Modifiers == ModifierKeys.Control)
            {
                RedoButton_Click(this, new RoutedEventArgs());
            }
        }
    }
}

using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Point = System.Windows.Point;

namespace SharpShot.UI
{
    public partial class BoundarySelectionWindow : Window
    {
        private Point _startPoint;
        private bool _isSelecting;
        public Rectangle? SelectedBoundary { get; private set; }
        private System.Drawing.Rectangle _targetBounds;

        private bool _shouldAccept = false;

        public BoundarySelectionWindow(System.Drawing.Rectangle targetBounds, string monitorName)
        {
            InitializeComponent();
            _targetBounds = targetBounds;
            Title = "Draw Boundary Box";
            
            // Position window to cover the target area (all monitors or specific monitor)
            Left = targetBounds.X;
            Top = targetBounds.Y;
            Width = targetBounds.Width;
            Height = targetBounds.Height;
            
            // Setup event handlers
            SelectionCanvas.MouseLeftButtonDown += OnMouseLeftButtonDown;
            SelectionCanvas.MouseLeftButtonUp += OnMouseLeftButtonUp;
            SelectionCanvas.MouseMove += OnMouseMove;
            KeyDown += OnKeyDown;
            PreviewKeyDown += OnKeyDown;
            
            // Handle closing to set DialogResult safely
            Closing += OnWindowClosing;
            
            Focusable = true;
        }

        private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Set DialogResult when closing, only if window was shown as dialog
            if (SelectedBoundary.HasValue && _shouldAccept)
            {
                try
                {
                    DialogResult = true;
                }
                catch
                {
                    // Ignore - caller will check SelectedBoundary
                }
            }
            else if (!_shouldAccept)
            {
                try
                {
                    DialogResult = false;
                }
                catch
                {
                    // Ignore
                }
            }
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(SelectionCanvas);
            _isSelecting = true;
            SelectionRect.Visibility = Visibility.Visible;
            
            Canvas.SetLeft(SelectionRect, _startPoint.X);
            Canvas.SetTop(SelectionRect, _startPoint.Y);
            SelectionRect.Width = 0;
            SelectionRect.Height = 0;
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isSelecting) return;
            
            _isSelecting = false;
            
            var endPoint = e.GetPosition(SelectionCanvas);
            
            var x = Math.Min(_startPoint.X, endPoint.X);
            var y = Math.Min(_startPoint.Y, endPoint.Y);
            var width = Math.Abs(endPoint.X - _startPoint.X);
            var height = Math.Abs(endPoint.Y - _startPoint.Y);
            
            if (width > 10 && height > 10)
            {
                // Convert window coordinates to screen coordinates
                var screenX = (int)(Left + x);
                var screenY = (int)(Top + y);
                
                SelectedBoundary = new Rectangle(screenX, screenY, (int)width, (int)height);
                _shouldAccept = true;
                
                // Close the window - DialogResult will be set in Closing event
                Close();
            }
            else
            {
                SelectionRect.Visibility = Visibility.Collapsed;
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isSelecting) return;
            
            var currentPoint = e.GetPosition(SelectionCanvas);
            
            var x = Math.Min(_startPoint.X, currentPoint.X);
            var y = Math.Min(_startPoint.Y, currentPoint.Y);
            var width = Math.Abs(currentPoint.X - _startPoint.X);
            var height = Math.Abs(currentPoint.Y - _startPoint.Y);
            
            Canvas.SetLeft(SelectionRect, x);
            Canvas.SetTop(SelectionRect, y);
            SelectionRect.Width = width;
            SelectionRect.Height = height;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                SelectedBoundary = null;
                _shouldAccept = false;
                
                // Close the window - DialogResult will be set in Closing event
                Close();
            }
        }
    }
}


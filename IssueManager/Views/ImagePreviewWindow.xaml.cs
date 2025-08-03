using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace IssueManager.Views
{
    public partial class ImagePreviewWindow : Window
    {
        private static ImagePreviewWindow _openInstance;

        private readonly List<BitmapImage> _images;
        private int _currentIndex = 0;
        public Visibility GalleryControlsVisibility => _images.Count > 1 ? Visibility.Visible : Visibility.Collapsed;

        public static void ShowSingle(List<BitmapImage> images, int startIndex = 0)
        {
            if (_openInstance != null)
            {
                _openInstance.Activate();
                _openInstance.SetImages(images, startIndex);
                return;
            }
            _openInstance = new ImagePreviewWindow(images, startIndex);
            _openInstance.Show();
            _openInstance.Closed += (s, e) => _openInstance = null;
        }

        // NEW: Accepts a list of images and the start index
        public ImagePreviewWindow(List<BitmapImage> images, int startIndex = 0)
        {
            InitializeComponent();
            _images = images ?? new List<BitmapImage>();
            _currentIndex = startIndex >= 0 && startIndex < _images.Count ? startIndex : 0;
            SetImage(_currentIndex);
        }
        private void SetImage(int index)
        {
            if (_images.Count == 0) return;
            _currentIndex = (index + _images.Count) % _images.Count; // Wrap around
            PreviewImage.Source = _images[_currentIndex];
            ImageIndexText.Text = $"{_currentIndex + 1} / {_images.Count}";
            UpdateGalleryButtons(); // <-- Add this line
        }
        public void SetImages(List<BitmapImage> images, int index)
        {
            _images.Clear();
            _images.AddRange(images);
            SetImage(index);
            UpdateGalleryButtons(); // <-- Add this (optional if already in SetImage)
        }

        private void Prev_Click(object sender, RoutedEventArgs e) => SetImage(_currentIndex - 1);
        private void Next_Click(object sender, RoutedEventArgs e) => SetImage(_currentIndex + 1);

        private void UpdateGalleryButtons()
        {
            var show = _images.Count > 1 ? Visibility.Visible : Visibility.Collapsed;

            PrevButton.Visibility = show;
            NextButton.Visibility = show;
            ImageIndexText.Visibility = show;

            // Optionally, also update enabled state for smoother UX (optional)
            PrevButton.IsEnabled = _images.Count > 1 && _currentIndex > 0;
            NextButton.IsEnabled = _images.Count > 1 && _currentIndex < _images.Count - 1;

            ImageIndexText.Text = $"{_currentIndex + 1} / {_images.Count}";
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }
    }
}

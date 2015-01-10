using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// 空白頁項目範本已記錄在 http://go.microsoft.com/fwlink/?LinkId=234238

namespace PhotoRaB2it
{
    /// <summary>
    /// 可以在本身使用或巡覽至框架內的空白頁面。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        List<FlipItem> listItems = new List<FlipItem>();
        List<Button> listThumbsButton = new List<Button>();
        List<String> listUnWantedFileNames = new List<string>();
        DataTemplate flipviewtemplate;
        StorageFolder selectedfolder;

        class FlipItem
        {
            Windows.Storage.Streams.IRandomAccessStream stream;
            public int Id { get; set; }
            public String Name { get; set; }
            public BitmapImage DisplayBitmap { get; set; }
            public Windows.Graphics.Imaging.ImageStream ThumbnailStream { get; set; }
            public Windows.Storage.Streams.InMemoryRandomAccessStream DecodedStream { get; set; }
            public bool IsKeep = false;
            /*
            public delegate void UpdateHandler(object sender);
            public event UpdateHandler Update;
            */
            public FlipItem(int Id, String Name, Windows.Storage.Streams.IRandomAccessStream stream)
            {
                this.Id = Id;
                this.stream = stream;
                this.Name = Name;
                this.DisplayBitmap = new BitmapImage();
            }

            public async System.Threading.Tasks.Task DecodeThumbnail()
            {
                try
                {
                    stream.Seek(0);
                    var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);
                    ThumbnailStream = await decoder.GetThumbnailAsync();
                    DisplayBitmap.SetSource(ThumbnailStream);
                    if (DecodedStream != null)
                        DecodedStream.Dispose();
                    GC.Collect();
                }
                catch (System.Threading.Tasks.TaskCanceledException ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                }
            }

            private double GetScaleFactor(int desired, uint h, uint w)
            {
                int longedge = GetLongEdge(h, w);
                double factor;
                factor = (longedge == h) ? (double)desired / (double)h : (double)desired / (double)w;
                return factor;
            }

            private int GetLongEdge(uint h, uint w)
            {
                return (int)Math.Max(h, w);
            }

            public async System.Threading.Tasks.Task DecodeImage()
            {
                try
                {
                    stream.Seek(0);
                    DecodedStream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                    var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);
                    var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateForTranscodingAsync(DecodedStream, decoder);

                    double factor = GetScaleFactor((int)Window.Current.Bounds.Width, decoder.PixelHeight, decoder.PixelWidth);
                    // Scaling occurs before flip/rotation.
                    encoder.BitmapTransform.ScaledHeight = (uint)(decoder.PixelHeight * factor);
                    encoder.BitmapTransform.ScaledWidth = (uint)(decoder.PixelWidth * factor);

                    //encoder.BitmapTransform.Rotation = Windows.Graphics.Imaging.BitmapRotation.Clockwise90Degrees;

                    // Fant is a relatively high quality interpolation algorithm.
                    encoder.BitmapTransform.InterpolationMode = Windows.Graphics.Imaging.BitmapInterpolationMode.Fant;

                    // Attempt to generate a new thumbnail from the updated pixel data.
                    // Note: Only JPEG, TIFF and JPEG-XR images support encoding thumbnails.
                    encoder.IsThumbnailGenerated = true;

                    try
                    {
                        await encoder.FlushAsync();
                    }
                    catch (Exception err)
                    {
                        switch (err.HResult)
                        {
                            case unchecked((int)0x88982F81): //WINCODEC_ERR_UNSUPPORTEDOPERATION
                                // If the encoder does not support writing a thumbnail, then try again
                                // but disable thumbnail generation.
                                encoder.IsThumbnailGenerated = false;
                                break;
                            default:
                                throw err;
                        }
                    }

                    if (encoder.IsThumbnailGenerated == false)
                    {
                        await encoder.FlushAsync();
                    }

                    DecodedStream.Seek(0);
                    DisplayBitmap.SetSource(DecodedStream);
                }
                catch (OutOfMemoryException ex)
                {
                    if (DecodedStream != null)
                        DecodedStream.Dispose();
                    GC.Collect();
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                }
                catch (System.Threading.Tasks.TaskCanceledException ex)
                {
                    DisplayBitmap.SetSource(ThumbnailStream);
                    if (DecodedStream != null)
                        DecodedStream.Dispose();
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                }
            }
        }

        public MainPage()
        {
            this.InitializeComponent();
            flipviewtemplate = flip.ItemTemplate;
            toggleKeepPhoto.IsEnabled = false;
            btnOrganize.IsEnabled = false;
            /*
            BitmapImage bi = new BitmapImage(new Uri("ms-appx:///Icons/FolderW.png"));
            imgtest.Source = bi;
            */
        }

        /// <summary>
        /// 在此頁面即將顯示在框架中時叫用。
        /// </summary>
        /// <param name="e">描述如何到達此頁面的事件資料。Parameter
        /// 屬性通常用來設定頁面。</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
        }

        private async void btnBrowseFolderClick(object sender, RoutedEventArgs e)
        {
            FolderPicker openFolder = new FolderPicker();
            openFolder.ViewMode = PickerViewMode.Thumbnail;
            openFolder.FileTypeFilter.Add(".jpg");
            openFolder.SuggestedStartLocation = PickerLocationId.ComputerFolder;
            StorageFolder folder = await openFolder.PickSingleFolderAsync();
            selectedfolder = folder;
            if (folder != null)
            {
                //MessageDialog md = new MessageDialog(folder.Path);
                //await md.ShowAsync();
                int fileCnt = 0;
                var files = await folder.GetFilesAsync();
                foreach (StorageFile file in files)
                {
                    if (file.FileType.ToLower() == ".jpg")
                    {
                        fileCnt++;
                    }
                }

                if (fileCnt <= 0) return;

                listItems.Clear();
                listThumbsButton.Clear();
                filmstrip.Children.Clear();

                maingrid.Children.Remove(flip);
                flip = new FlipView();
                maingrid.Children.Add(flip);
                flip.ItemTemplate = flipviewtemplate;
                flip.SelectionChanged -= flip_SelectionChanged;
                flip.ItemsSource = null;
                flip.SelectedIndex = -1;

                tb_current.Text = String.Format("{0:0000}", 1);
                tb_max.Text = String.Format("{0:0000}", fileCnt + 1);
                progressbar.Minimum = 0;
                progressbar.Maximum = fileCnt;
                panelProgress.Visibility = Windows.UI.Xaml.Visibility.Visible;
                int Id = 0;
                foreach (StorageFile file in files)
                {
                    if (file.FileType.ToLower() == ".jpg")
                    {
                        try
                        {
                            tb_current.Text = String.Format("{0:0000}", Id);
                            var stream = await file.OpenAsync(FileAccessMode.Read);
                            FlipItem item = new FlipItem(Id, file.Name, stream);
                            await item.DecodeThumbnail();
                            listItems.Add(item);

                            item.ThumbnailStream.Seek(0);
                            BitmapImage bi = new BitmapImage();
                            bi.SetSource(item.ThumbnailStream);
                            //ImageBrush background = new ImageBrush();
                            //background.ImageSource = bi;
                            Image img = new Image() { Height = 100, Source = bi };
                            Button filmstripbutton = new Button()
                            {
                                Tag = Id,
                                Content = img,
                                Name = "filmstripImg_" + file.Name,
                                BorderThickness = new Thickness(0),          
                                Margin = new Thickness(3)
                            };
                            filmstripbutton.Click += filmstripbutton_Click;

                            filmstrip.Children.Add(filmstripbutton);
                            listThumbsButton.Add(filmstripbutton);
                            progressbar.Value = ++Id;

                            #region Sample of decode and encode to memory with scaling
                            /*
                        var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read);
                        var memStream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                        var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);
                        var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateForTranscodingAsync(memStream, decoder);

                        double factor = GetScaleFactor(640, decoder.PixelHeight, decoder.PixelWidth);
                        // Scaling occurs before flip/rotation.
                        encoder.BitmapTransform.ScaledHeight = (uint)(decoder.PixelHeight * factor);
                        encoder.BitmapTransform.ScaledWidth = (uint)(decoder.PixelWidth * factor);
                        //encoder.BitmapTransform.ScaledWidth = 640;
                        //encoder.BitmapTransform.ScaledHeight = 480;
                        
                        //encoder.BitmapTransform.Rotation = Windows.Graphics.Imaging.BitmapRotation.Clockwise90Degrees;

                        // Fant is a relatively high quality interpolation algorithm.
                        encoder.BitmapTransform.InterpolationMode = Windows.Graphics.Imaging.BitmapInterpolationMode.Fant;

                        // Attempt to generate a new thumbnail from the updated pixel data.
                        // Note: Only JPEG, TIFF and JPEG-XR images support encoding thumbnails.
                        encoder.IsThumbnailGenerated = true;

                        try
                        {
                            await encoder.FlushAsync();
                        }
                        catch (Exception err)
                        {
                            switch (err.HResult)
                            {
                                case unchecked((int)0x88982F81): //WINCODEC_ERR_UNSUPPORTEDOPERATION
                                    // If the encoder does not support writing a thumbnail, then try again
                                    // but disable thumbnail generation.
                                    encoder.IsThumbnailGenerated = false;
                                    break;
                                default:
                                    throw err;
                            }
                        }

                        if (encoder.IsThumbnailGenerated == false)
                        {
                            await encoder.FlushAsync();
                        }

                        var decoderAfter = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(memStream);


                        memStream.Seek(0);
                        BitmapImage bi = new BitmapImage();
                        bi.SetSource(memStream);
                        //image1.Source = bi;
                        */
                            #endregion

                            #region Sample of FilePicker and show it in Image control
                            /*
                        FileOpenPicker openPicker = new FileOpenPicker();
                        openPicker.ViewMode = PickerViewMode.Thumbnail;
                        openPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                        openPicker.FileTypeFilter.Add(".jpg");
                        openPicker.FileTypeFilter.Add(".jpeg");
                        openPicker.FileTypeFilter.Add(".png");
                        StorageFile file = await openPicker.PickSingleFileAsync();
                        //StorageFile file = await StorageFile.GetFileFromPathAsync("E:\\PA120083.jpg");
                        if (file != null)
                        {
                            // Application now has read/write access to the picked file 
                            Windows.Storage.Streams.IRandomAccessStreamWithContentType stream = await file.OpenReadAsync();
                            //image1.Source = new BitmapImage(new Uri(file.Path, UriKind.Absolute));
                            BitmapImage bi = new BitmapImage();
                            bi.SetSource(stream);
                            //image1.Source = bi;

                            List<Image> list = new List<Image>();
                            Image img = new Image();
                            img.Source = bi;
                            list.Add(img);
                            flip.ItemsSource = list;
                        }
                        else
                        {
                            //OutputTextBlock.Text = "Operation cancelled.";
                            System.Diagnostics.Debug.WriteLine("Operation cancelled.");
                        }
                        */
                            #endregion
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(ex.Message);
                        }
                    }
                }
                panelProgress.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                flip.ItemsSource = listItems;
                flip.SelectedIndex = -1;
                flip.SelectionChanged += flip_SelectionChanged;
                flip.SelectedIndex = 0;
                toggleKeepPhoto.IsEnabled = true;
                btnOrganize.IsEnabled = true;
                //listItems[0].DecodeImage();
            }
        }

        void filmstripbutton_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            int Id = (int)btn.Tag;
            flip.SelectedItem = listItems[Id];
        }

        private void flip_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("flip.selectedindex: " + flip.SelectedIndex);
                if (e.RemovedItems.Count > 0)
                {
                    if (e.RemovedItems[0] is FlipItem)
                    {
                        FlipItem itemRemoved = e.RemovedItems[0] as FlipItem;
                        //System.Diagnostics.Debug.WriteLine("restoring to thumbnail: " + itemRemoved.Id);
                        itemRemoved.DecodeThumbnail();
                    }
                }
                if (e.AddedItems.Count > 0)
                {
                    if (e.AddedItems[0] is FlipItem)
                    {
                        FlipItem itemAdded = e.AddedItems[0] as FlipItem;
                        //System.Diagnostics.Debug.WriteLine("decoding to fullsized: " + itemAdded.Id);
                        System.Diagnostics.Debug.WriteLine("DecodeImage");
                        itemAdded.DecodeImage();

                        Button imgbtn = listThumbsButton[itemAdded.Id] as Button;
                        MakeButtonSelected(imgbtn);
                        toggleKeepPhoto.IsOn = itemAdded.IsKeep;

                        Windows.UI.Xaml.Media.MatrixTransform gf = imgbtn.TransformToVisual(imgbtn.Parent as UIElement) as Windows.UI.Xaml.Media.MatrixTransform;
                        filmstripScroll.ScrollToHorizontalOffset((gf.Matrix.OffsetX - imgbtn.Margin.Left) - (Window.Current.Bounds.Width / 2));
                    }
                }
                this.UpdateLayout();
                this.InvalidateArrange();
            }
            catch (System.Threading.Tasks.TaskCanceledException ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
            catch (System.Runtime.InteropServices.COMException comex)
            {
                System.Diagnostics.Debug.WriteLine(comex.Message);
            }
        }

        private void MakeButtonSelected(Button imgbtn)
        {
            foreach (UIElement element in filmstrip.Children)
            {
                Button btn = element as Button;
                btn.BorderThickness = new Thickness(0);
            }
            imgbtn.BorderThickness = new Thickness(1);
        }

        private void toggleKeepPhoto_Toggled(object sender, RoutedEventArgs e)
        {
            ToggleSwitch ts = sender as ToggleSwitch;
            if (ts.IsOn)
            {
                FlipItem item = flip.SelectedItem as FlipItem;
                Button btn = listThumbsButton[item.Id] as Button;
                btn.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 150, 100, 0));
                item.IsKeep = true;
            }
            else
            {
                FlipItem item = flip.SelectedItem as FlipItem;
                Button btn = listThumbsButton[item.Id] as Button;
                btn.Background = null;
                item.IsKeep = false;
            }
        }

        private async void btnOrganize_Click(object sender, RoutedEventArgs e)
        {
            listUnWantedFileNames.Clear();

            foreach (FlipItem item in listItems)
            {
                if (!item.IsKeep)
                {
                    listUnWantedFileNames.Add(item.Name);
                }
            }

            StorageFolder recycled = await selectedfolder.CreateFolderAsync("Recycled", CreationCollisionOption.OpenIfExists);

            var files = await selectedfolder.GetFilesAsync();
            foreach (StorageFile file in files)
            {
                string name = file.Name;
                bool exist = listUnWantedFileNames.Exists(delegate(string p) { return p.Trim() == name; });
                if (exist)
                {
                    await file.MoveAsync(recycled);
                }
            }

            toggleKeepPhoto.IsEnabled = false;
            btnOrganize.IsEnabled = false;

            MessageDialog md = new MessageDialog("Done");
            await md.ShowAsync();
        }
    }
}

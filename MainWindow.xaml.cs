
using CefSharp;
using MediaToolkit;
using MediaToolkit.Model;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using VideoLibrary;
using NAudio;
using NAudio.Wave;

namespace MusicPlayer
{
    public class MusicItem
    {
        public string Title { get; set; }
        public string FilePath { get; set; }
        public MusicItem(string Title, string FilePath)
        {
            this.Title = Title;
            this.FilePath = FilePath;
        }
    }
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        DispatcherTimer Ticks = new DispatcherTimer();
        List<string> CurrentPlaylist = new List<string>();
        List<string> MusicList = new List<string>();
        int PlaylistCursor;
        List<string> SearchPaths = new List<string>();
        WaveOutEvent PlayerStream = new WaveOutEvent();
        Mp3FileReader AudioReader;
        public MainWindow()
        {
            InitializeComponent();
#if NAUDIO
            PlayerStream.DeviceNumber = -1;
            PlayerStream.PlaybackStopped += PlayerStreamPlaybackStopped;
#else
            player.LoadedBehavior = MediaState.Manual;
            player.UnloadedBehavior = MediaState.Stop;
            player.MediaOpened += PlayerMediaOpened;
            player.MediaEnded += PlayerMediaEnded;
#endif
            PlayToggleButton.Click += PlayToggleButton_Click;
            MusicListView.MouseDoubleClick += MusicListViewMouseDoubleClick;
            PlaylistView.MouseDoubleClick += PlaylistViewMouseDoubleClick;
            NextButton.Click += NextButtonClick;
            PlayToggleButton.IsEnabled = false;
            Closing += MainWindowClosing;
            ReadLastPlaylist();
            if (!File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\musics\")) Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\musics\");
            SearchPaths.Add(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\musics\");
            PlaylistCursor = 0;
            DownloadBtn.Click += DownloadBtnClick;
            Refresh();
        }

        private void PlayerStreamPlaybackStopped(object sender, StoppedEventArgs e)
        {
            while (true)
            {
                if (PlaylistCursor >= CurrentPlaylist.Count - 1)
                {
                    PlayToggleButton.Content = "\uE768";
                    return;
                }
                else if (!File.Exists(CurrentPlaylist[PlaylistCursor + 1]))
                {
                    CurrentPlaylist.RemoveAt(PlaylistCursor);
                    PlaylistView.Items.RemoveAt(PlaylistCursor);
                }
                else
                {
                    break;
                }
            }
            AudioReader.Dispose();
            PlaylistCursor++;
            AudioReader = new Mp3FileReader(CurrentPlaylist[PlaylistCursor]);
            PlayerStream.Init(AudioReader);
            PlayerStream.Play();
        }

        private void NextButtonClick(object sender, RoutedEventArgs e)
        {
#if NAUDIO
            if(AudioReader != null)
            {
                AudioReader.Position = AudioReader.Length;
            }
#else
            player.Position = player.NaturalDuration.TimeSpan;
#endif
        }

        private void DownloadBtnClick(object sender, RoutedEventArgs e)
        {
            Regex YtRegex = new Regex(@"http(?:s?):\/\/(?:www\.)?youtu(?:be\.com\/watch\?v=|\.be\/)([\w\-_]*)(&(amp;)?‌​[\w\?‌​=]*)?");
            if (!YtRegex.IsMatch(YoutubeBrowser.Address))
            {
                MessageBox.Show("올바른 유튜브 영상 링크가 아닙니다.");
                return;
            }
            new Action(async () =>
            {
                YouTube yt = YouTube.Default;
                var items = yt.GetAllVideos(YoutubeBrowser.Address).ToList();
                items = items.OrderByDescending(item => item.AudioBitrate).ToList();
                foreach (var item in items)
                {
                    Debug.WriteLine(item.FullName + " - " + item.AudioBitrate);
                }
                File.WriteAllBytes(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\musics\" + items[0].FullName, await items[0].GetBytesAsync());
                var inputFile = new MediaFile { Filename = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\musics\" + items[0].FullName };
                var outputFile = new MediaFile { Filename = $"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\musics\" + items[0].FullName}.mp3" };

                using (var engine = new Engine())
                {
                    engine.GetMetadata(inputFile);
                    engine.Convert(inputFile, outputFile);
                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\musics\" + items[0].FullName);
                    Refresh();
                }
            }).Invoke();
        }

        

        private void PlayerMediaEnded(object sender, RoutedEventArgs e)
        {
            while (true)
            {
                if(PlaylistCursor >= CurrentPlaylist.Count - 1)
                {
                    PlayToggleButton.Content = "\uE768";
                    return;
                }
                else if (!File.Exists(CurrentPlaylist[PlaylistCursor + 1]))
                {
                    CurrentPlaylist.RemoveAt(PlaylistCursor);
                    PlaylistView.Items.RemoveAt(PlaylistCursor);
                }
                else
                {
                    break;
                }
            }
            PlaylistCursor++;
            player.Source = new Uri(CurrentPlaylist[PlaylistCursor]);
            player.Play();       
        }

        private void PlaylistViewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            PlaylistCursor = PlaylistView.SelectedIndex;
            while(true)
            {
                if (!File.Exists(CurrentPlaylist[PlaylistCursor]))
                {
                    CurrentPlaylist.RemoveAt(PlaylistCursor);
                    PlaylistView.Items.RemoveAt(PlaylistCursor);
                }
                else
                {
                    break;
                }
            }
#if NAUDIO
            AudioReader = new Mp3FileReader(CurrentPlaylist[PlaylistCursor]);
            PlayerStream.Init(AudioReader);
            PlayerStream.Play();
            ProgressSlider.Maximum = AudioReader.TotalTime.TotalMilliseconds;
            TotalTimeLabel.Content = AudioReader.TotalTime.ToString(@"mm\:ss");
            NowPlayingLabel.Content = CurrentPlaylist[PlaylistCursor].Split('\\')[CurrentPlaylist[PlaylistCursor].Split('\\').Length - 1];
            Ticks.Interval = TimeSpan.FromMilliseconds(1);
            Ticks.Tick += Ticks_Tick;
            Ticks.Start();
            PlayToggleButton.Content = "\uE769";
            PlayToggleButton.IsEnabled = true;
#else
            player.Source = new Uri(CurrentPlaylist[PlaylistCursor], UriKind.Absolute);
            player.Play();
#endif
        }

        private void MusicListViewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            AddToPlaylist(MusicList[MusicListView.SelectedIndex]);
        }

        private void MainWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            File.WriteAllText("last.json", JArray.FromObject(CurrentPlaylist).ToString());
        }

        public void Refresh()
        {
            MusicList.Clear();
            foreach(string path in SearchPaths)
            {
                MusicList.AddRange(SearchForMusics(path));
            }
            MusicListView.Items.Clear();
            foreach(string path in MusicList)
            {
                MusicListView.Items.Add(new MusicItem(path.Split('\\')[path.Split('\\').Length - 1], path));
            }   
        }

        public void AddToPlaylist(string path)
        {
            if(CurrentPlaylist.Contains(path))
            {
                return;
            }
            CurrentPlaylist.Add(path);
            PlaylistView.Items.Add(new MusicItem(path.Split('\\')[path.Split('\\').Length - 1], path));
        }

        public List<string> SearchForMusics(string path)
        {
            List<string> arr = new List<string>();
            foreach (string f in Directory.GetFiles(path))
            {
                if(f.EndsWith(".mp3"))
                {
                    arr.Add(f);
                }
            }
            foreach (string d in Directory.GetDirectories(path))
            {
                arr.AddRange(SearchForMusics(d));
            }
            return arr;
        }

        public void ReadLastPlaylist()
        {
            if(!File.Exists("last.json"))
            {
                return;
            }
            JArray playlistArr = JArray.Parse(File.ReadAllText("last.json"));
            foreach(var playlistItem in playlistArr)
            {
                AddToPlaylist(playlistItem.ToObject<string>());
            }
        }

        private void PlayToggleButton_Click(object sender, RoutedEventArgs e)
        {
#if NAUDIO
            if(PlayerStream.PlaybackState == PlaybackState.Stopped || PlayerStream.PlaybackState == PlaybackState.Paused)
            {
                PlayToggleButton.Content = "\uE769";
                PlayerStream.Play();
            }
            else
            {
                PlayToggleButton.Content = "\uE768";
                PlayerStream.Pause();
            }
#else
            if(GetMediaState(player) == MediaState.Stop || GetMediaState(player) == MediaState.Pause)
            {
                PlayToggleButton.Content = "\uE769";
                player.Play();
            }
            else
            {
                PlayToggleButton.Content = "\uE768";
                player.Pause();
            }
#endif
        }

        private MediaState GetMediaState(MediaElement myMedia)
        {   
            FieldInfo hlp = typeof(MediaElement).GetField("_helper", BindingFlags.NonPublic | BindingFlags.Instance);
            object helperObject = hlp.GetValue(myMedia);
            FieldInfo stateField = helperObject.GetType().GetField("_currentState", BindingFlags.NonPublic | BindingFlags.Instance);
            MediaState state = (MediaState)stateField.GetValue(helperObject);
            return state;
        }

        private void PlayerMediaOpened(object sender, EventArgs e)
        {
#if NAUDIO
            
#else
            ProgressSlider.Maximum = player.NaturalDuration.TimeSpan.TotalMilliseconds;
            TotalTimeLabel.Content = player.NaturalDuration.TimeSpan.ToString(@"mm\:ss");
            NowPlayingLabel.Content = CurrentPlaylist[PlaylistCursor].Split('\\')[CurrentPlaylist[PlaylistCursor].Split('\\').Length - 1];
            Ticks.Interval = TimeSpan.FromMilliseconds(1);
            Ticks.Tick += Ticks_Tick;
            Ticks.Start();
            PlayToggleButton.Content = "\uE769";
            PlayToggleButton.IsEnabled = true;
            player.Play();
#endif
        }

        private void Ticks_Tick(object sender, EventArgs e)
        {
#if NAUDIO
            TimeSpan ProgressTimeSpan = new TimeSpan(AudioReader.Position);
            ProgressSlider.Value = ProgressTimeSpan.TotalMilliseconds;
            ProgressLabel.Content = ProgressTimeSpan.ToString(@"mm\:ss");
#else
            TimeSpan ProgressTimeSpan = player.Position;
            ProgressSlider.Value = ProgressTimeSpan.TotalMilliseconds;
            ProgressLabel.Content = ProgressTimeSpan.ToString(@"mm\:ss");
#endif
        }

        private void ProgressSliderDrag(object sender, EventArgs e) //드래그 끝
        {
            //드래그된 위치로 이동
            TimeSpan ProgressTimeSpan = new TimeSpan(0,0,0,0,(int)ProgressSlider.Value);
#if NAUDIO
            AudioReader.Position = ProgressTimeSpan.Ticks;
            ProgressLabel.Content = ProgressTimeSpan.ToString(@"mm\:ss");

            //이동 후 재생 재시작
            Ticks.Start();
            player.Play();
#else
            player.Position = ProgressTimeSpan;
            ProgressLabel.Content = ProgressTimeSpan.ToString(@"mm\:ss");

            //이동 후 재생 재시작
            Ticks.Start();
            player.Play();
#endif

        }

        private void ProgressSliderDragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)    //드래그 시작
        {
            //드래그중 재생 멈춤
            Ticks.Stop();
#if NAUDIO
            PlayerStream.Pause();
#else
            player.Pause();
#endif
        }

        private void ProgressSliderDragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)    //드래그 중
        {
            //현재 위치 업데이트
            TimeSpan ProgressTimeSpan = new TimeSpan(0, 0, 0, 0, (int)ProgressSlider.Value);
            ProgressLabel.Content = ProgressTimeSpan.ToString(@"mm\:ss");
        }

        private void PlaylistMenuItemClick(object sender, RoutedEventArgs e)
        {
            CurrentPlaylist.RemoveAt(PlaylistCursor);
            PlaylistView.Items.RemoveAt(PlaylistCursor);
        }

        private void VolumeSliderDragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            PlayerStream.Volume = (float)VolumeSlider.Value / 100;
            player.Volume = VolumeSlider.Value / 100;
        }
    }
}

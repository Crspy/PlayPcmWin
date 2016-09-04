﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.ComponentModel;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Globalization;
using Wasapi;

namespace PlayPcmWinAlbum {
    public partial class MainWindow : Window {
        private List<AlbumTile> mTileItems = new List<AlbumTile>();
        private Size mTileSize = new Size(256, 256);
        private CancellationTokenSource mAppExitToken = new CancellationTokenSource();
        private ContentList mContentList = new ContentList();
        private DataGridPlayListHandler mDataGridPlayListHandler;
        private PlaybackController mPlaybackController = new PlaybackController();
        private bool mInitialized = false;
        private BackgroundWorker mBackgroundLoad;
        private BackgroundWorker mBackgroundPlay;
        private string mPreferredDeviceIdString = "";
        private const int PROGRESS_REPORT_INTERVAL_MS = 100;
        private const int SLIDER_UPDATE_TICKS = 500;

        private const string PLAYING_TIME_UNKNOWN = "--:-- / --:--";
        private const string PLAYING_TIME_ALLZERO = "00:00 / 00:00";

        private enum State {
            Init,
            ReadContentList,
            CreateContentList,
            AlbumBrowsing,
            AlbumTrackBrowsing,
        };

        private State mState = State.Init;
        private BackgroundContentListBuilder mBwContentListBuilder;

        public MainWindow() {
            InitializeComponent();
            mDataGridPlayListHandler = new DataGridPlayListHandler(mDataGridPlayList);
            mLabelAlbumName.Content = "";
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            mInitialized = true;

            // アルバム一覧を読み出す。
            if (ReadContentList()) {
                UpdateContentList();
                ChangeDisplayState(State.AlbumBrowsing);
            } else {
                // アルバム一覧作成。
                if (!CreateContentList()) {
                    Close();
                    return;
                }
            }
            mPlaybackController.Init();
        }

        private void ChangeDisplayState(State t) {

            switch (t) {
            case State.Init:
            case State.CreateContentList:
            case State.ReadContentList:
                mAlbumScrollViewer.Visibility = System.Windows.Visibility.Visible;
                mDataGridPlayList.Visibility = System.Windows.Visibility.Hidden;
                mDockPanelPlayback.Visibility = System.Windows.Visibility.Hidden;
                mProgressBar.Visibility = Visibility.Collapsed;
                mTextBlockMessage.Visibility = Visibility.Visible;
                mMenuItemBack.IsEnabled = false;
                mMenuItemRefresh.IsEnabled = true;
                break;
            case State.AlbumBrowsing:
                mAlbumScrollViewer.Visibility = System.Windows.Visibility.Visible;
                mDataGridPlayList.Visibility = System.Windows.Visibility.Hidden;
                mDockPanelPlayback.Visibility = System.Windows.Visibility.Hidden;
                mProgressBar.Visibility = Visibility.Collapsed;
                mTextBlockMessage.Visibility = Visibility.Collapsed;
                mMenuItemBack.IsEnabled = false;
                mMenuItemRefresh.IsEnabled = true;
                break;
            case State.AlbumTrackBrowsing:
                mAlbumScrollViewer.Visibility = System.Windows.Visibility.Hidden;
                mDataGridPlayList.Visibility = System.Windows.Visibility.Visible;
                mDockPanelPlayback.Visibility = System.Windows.Visibility.Visible;
                mProgressBar.Visibility = Visibility.Collapsed;
                mMenuItemBack.IsEnabled = true;
                mMenuItemRefresh.IsEnabled = false;
                break;
            }

            mState = t;
        }

        private void UpdatePlaybackControlState(PlaybackController.State state) {
            switch (state) {
            case PlaybackController.State.Stopped:
                mButtonPlay.IsEnabled = true;
                mButtonStop.IsEnabled = false;
                mProgressBar.Visibility = System.Windows.Visibility.Collapsed;
                mLabelPlayingTime.Content = PLAYING_TIME_ALLZERO;
                break;
            case PlaybackController.State.Playing:
                mButtonPlay.IsEnabled = false;
                mButtonStop.IsEnabled = true;
                mProgressBar.Visibility = System.Windows.Visibility.Collapsed;
                break;
            case PlaybackController.State.Loading:
                mButtonPlay.IsEnabled = false;
                mButtonStop.IsEnabled = false;
                mProgressBar.Visibility = System.Windows.Visibility.Visible;
                break;

            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }
        }

        private bool ReadContentList() {
            return mContentList.Load();
        }

        private bool CreateContentList() {
            mTilePanel.Clear();

            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.SelectedPath = "C:\\audio";
            if (System.Windows.Forms.DialogResult.OK != dialog.ShowDialog()) {
                return false;
            }

            mBwContentListBuilder = new BackgroundContentListBuilder(mContentList);
            mBwContentListBuilder.AddProgressChanged(OnBackgroundContentListBuilder_ProgressChanged);
            mBwContentListBuilder.AddRunWorkerCompleted(OnBackgroundContentListBuilder_RunWorkerCompleted);

#if false
            // バグっているときの調査用。
            var result = mBwContentListBuilder.BackgroundDoWorkImpl(dialog.SelectedPath, false);
#else
            // バックグラウンド実行。
            mBwContentListBuilder.RunWorkerAsync(dialog.SelectedPath);
            ChangeDisplayState(State.CreateContentList);
#endif
            return true;
        }

        private void OnBackgroundContentListBuilder_ProgressChanged(object sender, ProgressChangedEventArgs e) {
            var rpa = (BackgroundContentListBuilder.ReportProgressArgs)e.UserState;

            mTextBlockMessage.Text = rpa.text;
            mProgressBar.Value = e.ProgressPercentage;
        }

        private void OnBackgroundContentListBuilder_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            var result = (BackgroundContentListBuilder.RunWorkerCompletedResult)e.Result;

            ChangeDisplayState(State.AlbumBrowsing);

            if (result == null) {
                Console.WriteLine("Error");

            } else if (result.fileCount == 0) {
                MessageBox.Show(string.Format(Properties.Resources.ErrorMusicFileNotFound, result.path));
                Close();
            } else {
                mContentList.Save();
                UpdateContentList();
            }
        }

        private void UpdateContentList() {
            AlbumTile.UpdateTileSize(mTileSize);

            mTilePanel.Clear();

            mTilePanel.UpdateTileSize(mTileSize);
            for (int i=0; i < mContentList.AlbumCount; ++i) {
                var album = mContentList.AlbumNth(i);
                var tic = new TiledItemContent(album.Name, album.AudioFileNth(0).AlbumCoverArt, album);
                var tileItem = new AlbumTile(tic, OnAlbumTileClicked, mAppExitToken.Token);

                mTilePanel.AddVirtualChild(tileItem);
                mTileItems.Add(tileItem);
            }
            mTilePanel.UpdateChildPosition();
        }

        private void CancelAll() {
            if (mAppExitToken != null) {
                mAppExitToken.Cancel();
                mAppExitToken = null;
            }
        }

        private void Window_Closed(object sender, EventArgs e) {
            CancelAll();

            mPlaybackController.Stop();
            mPlaybackController.Term();
        }

        private void OnAlbumTileClicked(AlbumTile sender, TiledItemContent content) {
            Console.WriteLine("clicked {0}", content.DisplayName);
            var album = content.Tag as ContentList.Album;
            ShowAlbum(album);
        }

        private void DispCoverArt(byte[] albumCoverArt) {
            if (albumCoverArt.Length == 0) {
                mImageCoverArt.Source = null;
                mImageCoverArt.Visibility = System.Windows.Visibility.Collapsed;
            } else {
                try {
                    using (var stream = new MemoryStream(albumCoverArt)) {
                        BitmapImage bi = new BitmapImage();
                        bi.BeginInit();
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.UriSource = null;
                        bi.StreamSource = stream;
                        bi.EndInit();

                        mImageCoverArt.Source = bi;
                        mImageCoverArt.Visibility = System.Windows.Visibility.Visible;
                    }
                } catch (IOException ex) {
                    Console.WriteLine("D: DispCoverart {0}", ex);
                    mImageCoverArt.Source = null;
                } catch (System.IO.FileFormatException ex) {
                    Console.WriteLine("D: DispCoverart {0}", ex);
                    mImageCoverArt.Source = null;
                }
            }
        }

        private void UpdateDeviceList() {
            mListBoxPlaybackDevice.Items.Clear();
            mPlaybackController.EnumerateDevices();
            if (mPlaybackController.GetDeviceCount() == 0) {
                MessageBox.Show("Error: playback device not found");
                Close();
            }

            for (int i = 0; i < mPlaybackController.GetDeviceCount(); ++i) {
                var attr = mPlaybackController.GetDeviceAttribute(i);
                mListBoxPlaybackDevice.Items.Add(attr.Name);
                if (0 == string.Compare(mPreferredDeviceIdString, attr.DeviceIdString)) {
                    mListBoxPlaybackDevice.SelectedIndex = i;
                }
            }

            if (mListBoxPlaybackDevice.SelectedIndex < 0) {
                mListBoxPlaybackDevice.SelectedIndex = 0;
                mPreferredDeviceIdString = mPlaybackController.GetDeviceAttribute(0).DeviceIdString;
            }
        }

        private void ShowAlbum(ContentList.Album album) {
            album.UpdateIds();
            mContentList.AlbumSelected(album);

            UpdateDeviceList();

            var albumCoverArt = album.AudioFileNth(0).AlbumCoverArt;
            DispCoverArt(albumCoverArt);

            mLabelAlbumName.Content = album.Name;
            mDataGridPlayListHandler.ShowAlbum(album);
            ChangeDisplayState(State.AlbumTrackBrowsing);
            UpdatePlaybackControlState(PlaybackController.State.Stopped);
        }

        private void OnMenuItemBack_Click(object sender, RoutedEventArgs e) {
            mPlaybackController.Stop();
            mLabelAlbumName.Content = "";
            ChangeDisplayState(State.AlbumBrowsing);
        }

        private void OnMenuItemRefresh_Click(object sender, RoutedEventArgs e) {
            if (!CreateContentList()) {
                Close();
                return;
            }
        }

        private void OnDataGridPlayList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) {
            Console.WriteLine("DataGridPlayList_SelectionChanged()");
            if (PlaybackController.State.Playing == mPlaybackController.GetState()) {
                // 再生中に曲選択。
                mPlaybackController.Play(mDataGridPlayList.SelectedIndex);
            }
        }

        private void OnMenuItemSettings_Click(object sender, RoutedEventArgs e) {

        }

        class BackgroundLoadArgs {
            public ContentList.Album Album { get; set;}
            public int First { get; set; }
            public int DeviceIdx { get; set; }
            public BackgroundLoadArgs(ContentList.Album album, int first, int deviceIdx) {
                Album = album;
                First = first;
                DeviceIdx = deviceIdx;
            }
        };

        class BackgroundLoadResult {
            public BackgroundLoadArgs Args { get; set; }
            public bool Result { get; set; }
            public BackgroundLoadResult(BackgroundLoadArgs args, bool result) {
                Args = args;
                Result = result;
            }
        };

        private void OnButtonPlay_Click(object sender, RoutedEventArgs e) {
            var args = new BackgroundLoadArgs(
                    mContentList.GetSelectedAlbum(), mDataGridPlayList.SelectedIndex, mListBoxPlaybackDevice.SelectedIndex);

            var playList = CreatePlayList(args.Album, args.First);
            bool result = mPlaybackController.PlaylistCreateStart(args.DeviceIdx, args.Album.AudioFileNth(args.First));
            if (!result) {
                MessageBox.Show("Error: Playback start failed!");
                return;
            }

            UpdatePlaybackControlState(PlaybackController.State.Loading);
            mBackgroundLoad = new BackgroundWorker();
            mBackgroundLoad.WorkerReportsProgress = true;
            mBackgroundLoad.DoWork += new DoWorkEventHandler(OnBackgroundLoad_DoWork);
            mBackgroundLoad.ProgressChanged += new ProgressChangedEventHandler(OnBackgroundLoad_ProgressChanged);
            mBackgroundLoad.RunWorkerCompleted += new RunWorkerCompletedEventHandler(OnBackgroundLoad_RunWorkerCompleted);
            mBackgroundLoad.RunWorkerAsync(args);
        }

        /// <summary>
        /// album[first]と同一グループのファイル一覧作成。
        /// </summary>
        private static List<ContentList.AudioFile> CreatePlayList(ContentList.Album album, int first) {
            var afList = new List<ContentList.AudioFile>();
            var firstAf = album.AudioFileNth(first);

            int groupId = firstAf.GroupId;

            for (int i = 0; i < album.AudioFileCount; ++i) {
                var af = album.AudioFileNth(i);

                if (groupId == af.GroupId) {
                    afList.Add(af);
                }
            }
            return afList;
        }

        void OnBackgroundLoad_DoWork(object sender, DoWorkEventArgs e) {
            var args = e.Argument as BackgroundLoadArgs;

            var playList = CreatePlayList(args.Album, args.First);

            int added = 0;
            for (int i = 0; i < playList.Count; ++i) {
                var af = playList[i];
                if (mPlaybackController.Add(af)) {
                    ++added;
                }
                mBackgroundLoad.ReportProgress((i + 1) * 100 / playList.Count);
            }
            mPlaybackController.PlaylistCreateEnd();

            e.Result = new BackgroundLoadResult(args, 0 < added);
        }

        void OnBackgroundLoad_ProgressChanged(object sender, ProgressChangedEventArgs e) {
            mProgressBar.Value = e.ProgressPercentage;
        }

        void OnBackgroundLoad_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            var result = e.Result as BackgroundLoadResult;
            if (result.Result) {
                mPlaybackController.Play(result.Args.First);
            } else {
                MessageBox.Show("Error: File load failed!");
            }

            UpdatePlaybackControlState(mPlaybackController.GetState());
            mBackgroundPlay = new BackgroundWorker();
            mBackgroundPlay.WorkerReportsProgress = true;
            mBackgroundPlay.ProgressChanged += new ProgressChangedEventHandler(OnBackgroundPlay_ProgressChanged);
            mBackgroundPlay.DoWork += new DoWorkEventHandler(OnBackgroundPlay_DoWork);
            mBackgroundPlay.RunWorkerCompleted += new RunWorkerCompletedEventHandler(OnBackgroundPlay_RunWorkerCompleted);
            mBackgroundPlay.RunWorkerAsync();
        }

        void OnBackgroundPlay_DoWork(object sender, DoWorkEventArgs e) {
            bool bEnd = true;
            do {
                mBackgroundPlay.ReportProgress(0);
                bEnd = mPlaybackController.Run(PROGRESS_REPORT_INTERVAL_MS);
            } while (!bEnd);
        }

        long mLastSliderPositionUpdateTime = 0;

        void OnBackgroundPlay_ProgressChanged(object sender, ProgressChangedEventArgs e) {
            // 再生中PCMデータ(または一時停止再開時再生予定PCMデータ等)の再生位置情報を画面に表示する。
            WasapiCS.PcmDataUsageType usageType = WasapiCS.PcmDataUsageType.NowPlaying;
            int pcmDataId = mPlaybackController.GetPcmDataId(WasapiCS.PcmDataUsageType.NowPlaying);
            if (pcmDataId < 0) {
                pcmDataId = mPlaybackController.GetPcmDataId(WasapiCS.PcmDataUsageType.PauseResumeToPlay);
                usageType = WasapiCS.PcmDataUsageType.PauseResumeToPlay;
            }
            if (pcmDataId < 0) {
                pcmDataId = mPlaybackController.GetPcmDataId(WasapiCS.PcmDataUsageType.SpliceNext);
                usageType = WasapiCS.PcmDataUsageType.SpliceNext;
            } 
            
            string playingTimeString = string.Empty;
            if (pcmDataId < 0) {
                playingTimeString = PLAYING_TIME_UNKNOWN;
            } else {
                /*
                if (mDataGridPlayList.SelectedIndex != GetPlayListIndexOfPcmDataId(pcmDataId)) {
                    mDataGridPlayList.SelectedIndex = GetPlayListIndexOfPcmDataId(pcmDataId);
                    mDataGridPlayList.ScrollIntoView(dataGridPlayList.SelectedItem);
                }
                */

                var playPos = mPlaybackController.GetCursorLocation(usageType);
                var stat = mPlaybackController.GetSessionStatus();

                long now = DateTime.Now.Ticks;
                if (now - mLastSliderPositionUpdateTime > SLIDER_UPDATE_TICKS) {
                    // スライダー位置の更新。0.5秒に1回
                    mSlider1.Maximum = playPos.TotalFrameNum;
                    if (!mSliderSliding || playPos.TotalFrameNum <= mSlider1.Value) {
                        mSlider1.Value = playPos.PosFrame;
                    }
                    mLastSliderPositionUpdateTime = now;
                }

                playingTimeString = string.Format(CultureInfo.InvariantCulture, "{0} / {1}",
                        Util.SecondsToMSString((int)(playPos.PosFrame / stat.DeviceSampleRate)),
                        Util.SecondsToMSString((int)(playPos.TotalFrameNum / stat.DeviceSampleRate)));
            }

            // 再生時間表示の再描画をできるだけ抑制する。負荷が減る効果がある
            if (playingTimeString != string.Empty && 0 != string.Compare((string)mLabelPlayingTime.Content, playingTimeString)) {
                mLabelPlayingTime.Content = playingTimeString;
            } else {
                //System.Console.WriteLine("time disp update skipped");
            }
        }

        void OnBackgroundPlay_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            UpdatePlaybackControlState(mPlaybackController.GetState());
        }

        private void OnButtonStop_Click(object sender, RoutedEventArgs e) {
            mPlaybackController.Stop();
            UpdatePlaybackControlState(PlaybackController.State.Stopped);
        }

        private void OnButtonPause_Click(object sender, RoutedEventArgs e) {

        }

        private void OnButtonPrev_Click(object sender, RoutedEventArgs e) {

        }

        private void OnButtonNext_Click(object sender, RoutedEventArgs e) {

        }

        private bool mSliderSliding = false;
        private long mLastSliderValue = 0;

        private void OnSlider1_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (e.Source != mSlider1) {
                return;
            }

            mLastSliderValue = (long)mSlider1.Value;
            mSliderSliding = true;
        }

        private void OnSlider1_MouseMove(object sender, MouseEventArgs e) {
            if (e.Source != mSlider1) {
                return;
            }

            if (e.LeftButton == MouseButtonState.Pressed) {
                mLastSliderValue = (long)mSlider1.Value;
                if (!mButtonPlay.IsEnabled) {
                    mPlaybackController.SetPosFrame((long)mSlider1.Value);
                }
            }
        }
        private void OnSlider1_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            if (e.Source != mSlider1) {
                return;
            }

            if (!mButtonPlay.IsEnabled &&
                    mLastSliderValue != (long)mSlider1.Value) {
                mPlaybackController.SetPosFrame((long)mSlider1.Value);
            }

            mLastSliderValue = 0;
            mSliderSliding = false;
        }

        private void OnListBoxPlaybackDevice_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) {
            if (!mInitialized) {
                return;
            }

            mPreferredDeviceIdString = mPlaybackController.GetDeviceAttribute(mListBoxPlaybackDevice.SelectedIndex).DeviceIdString;
        }
    }
}

﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using WWIIRFilterDesign;
using WWMath;
using WWUtil;

namespace WWOfflineResampler {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        private enum State {
            NotReady,
            Ready,
            ReadFile,
            FilterDesigned,
            Converting,
            WriteFile
        }

        private State mState = State.NotReady;
        private bool mInitialized = false;
        private BackgroundWorker mBw = new BackgroundWorker();

        private static int [] mTargetSampleRateList = {
            32000,
            44100,
            48000,
            64000,
            88200,
            96000,
            128000,
            176400,
            192000,
            352800,
            384000,
        };
        
        Main mMain = new Main();

        public MainWindow() {
            InitializeComponent();

            if (mMain.ParseCommandLine()) {
                Application.Current.Shutdown();
                return;
            }

            mPoleZeroPlotZ.Mode = WWUserControls.PoleZeroPlot.ModeType.ZPlane;
            mPoleZeroPlotZ.Update();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            mBw.DoWork += new DoWorkEventHandler(mBw_DoWork);
            mBw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(mBw_RunWorkerCompleted);
            mBw.WorkerReportsProgress = true;
            mBw.ProgressChanged += new ProgressChangedEventHandler(mBw_ProgressChanged);
            
            mInitialized = true;

            mState = State.Ready;
            Update();

            /* サンプルレートの比の計算のテスト。
            foreach (int a in mTargetSampleRateList) {
                foreach (int b in mTargetSampleRateList) {
                    long lcm = WWMath.Functions.LCM(a, b);
                    Console.WriteLine("LCM({0}, {1}) = {2},  {3}/{4}", a, b,
                        lcm, lcm/a, lcm/b);
                }
            }
            */
        }

        private void buttonBrowseInputFile_Click(object sender, RoutedEventArgs e) {
            var dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = Properties.Resources.FilterReadAudioFiles;
            dlg.ValidateNames = true;

            var result = dlg.ShowDialog();
            if (result != true) {
                return;
            }

            textBoxInputFile.Text = dlg.FileName;
            InputFormUpdated();
        }

        private void buttonBrowseOutputFile_Click(object sender, RoutedEventArgs e) {
            var dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.Filter = Properties.Resources.FilterWriteAudioFiles;
            dlg.ValidateNames = true;

            var result = dlg.ShowDialog();
            if (result != true) {
                return;
            }

            textBoxOutputFile.Text = dlg.FileName;
            InputFormUpdated();
        }

        private void InputFormUpdated() {
            if (0 < textBoxInputFile.Text.Length &&
                    0 < textBoxOutputFile.Text.Length) {
                mState = State.Ready;
            } else {
                mState = State.NotReady;
            }

            Update();
        }

        private void Update() {
            if (!mInitialized) {
                return;
            }

            switch (mState) {
            case State.NotReady:
                buttonStartConversion.IsEnabled = false;
                break;
            case State.Ready:
                buttonStartConversion.IsEnabled = true;
                break;
            case State.ReadFile:
            case State.FilterDesigned:
            case State.Converting:
            case State.WriteFile:
                buttonStartConversion.IsEnabled = false;
                break;
            }
        }

        void mBw_DoWork(object sender, DoWorkEventArgs e) {
            var param = e.Argument as Main.BWStartParams;
            var result = mMain.DoWork(param,
                (int percent, Main.BWProgressParam p) => { mBw.ReportProgress(percent, p); });
            e.Result = result;
        }

        void mBw_ProgressChanged(object sender, ProgressChangedEventArgs e) {
            var param = e.UserState as Main.BWProgressParam;

            textBoxLog.Text += param.message;
            textBoxLog.ScrollToEnd();
            switch (param.state) {
            case Main.State.Started:
            case Main.State.ReadFile:
                mState = State.ReadFile;
                break;
            case Main.State.FilterDesigned:
                mState = State.FilterDesigned;
                break;
            case Main.State.Converting:
                mState = State.Converting;
                break;
            case Main.State.WriteFile:
                mState = State.WriteFile;
                break;
            case Main.State.Finished:
                mState = State.Ready;
                break;
            }
            progressBar1.Value = e.ProgressPercentage;

            if (mState == State.FilterDesigned) {
                mTimeDomainPlot.ImpulseResponseFunction = mMain.Afd().ImpulseResponseFunction;
                mTimeDomainPlot.StepResponseFunction = mMain.Afd().UnitStepResponseFunction;
                mTimeDomainPlot.TimeScale = mMain.Afd().TimeDomainFunctionTimeScale;
                mTimeDomainPlot.Update();

                mPoleZeroPlotZ.Mode = WWUserControls.PoleZeroPlot.ModeType.ZPlane;
                mPoleZeroPlotZ.TransferFunction = mMain.IIRiim().TransferFunction;
                mPoleZeroPlotZ.Update();

                mFrequencyResponseZ.Mode = WWUserControls.FrequencyResponse.ModeType.ZPlane;
                mFrequencyResponseZ.SamplingFrequency = mMain.IIRiim().SamplingFrequency();
                mFrequencyResponseZ.TransferFunction = mMain.IIRiim().TransferFunction;
                mFrequencyResponseZ.Update();
            }

            Update();
        }

        void mBw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            var param = e.Result as Main.BWCompletedParam;
            mState = State.Ready;

            mStopwatch.Stop();
            textBoxLog.Text += param.message;
            textBoxLog.Text += string.Format("Finished. elapsed time: {0} sec\n", (mStopwatch.ElapsedMilliseconds/100) * 0.1);
            textBoxLog.ScrollToEnd();

            Update();
            progressBar1.Value = 0;
        }

        Stopwatch mStopwatch = new Stopwatch();

        private void buttonStart_Click(object sender, RoutedEventArgs e) {
            int targetSampleRate = mTargetSampleRateList[comboBoxTargetSampleRate.SelectedIndex];

            mState = State.ReadFile;
            Update();

            progressBar1.Value = Main.START_PERCENT;

            mStopwatch.Reset();
            mStopwatch.Start();

            mBw.RunWorkerAsync(new Main.BWStartParams(textBoxInputFile.Text, targetSampleRate, textBoxOutputFile.Text));
        }

    }
}
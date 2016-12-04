﻿using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using WWMath;

namespace WWUserControls {
    /// <summary>
    /// Interaction logic for FrequencyResponse.xaml
    /// </summary>
    public partial class FrequencyResponse : UserControl {
        public FrequencyResponse() {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e) {
            FreqScale = FreqScaleType.Logarithmic;
            FreqRange = FreqRangeType.SF_10HzTo100kHz;
            MagnitudeScale = MagScaleType.Logarithmic;
            MagnitudeRange = MagnitudeRangeType.M96dB;
            ShowGain = true;
            ShowPhase = true;
            PhaseShiftDegree = -180;

            Update();
        }

        private const int FR_LINE_LEFT = 64;
        private const int FR_LINE_HEIGHT = 256;
        private const int FR_LINE_WIDTH = 512;
        private const int FR_LINE_TOP = 32;
        private const int FR_LINE_BOTTOM = FR_LINE_TOP + FR_LINE_HEIGHT;
        private const int FR_LINE_YCENTER = (FR_LINE_TOP + FR_LINE_BOTTOM) / 2;

        public enum FreqScaleType {
            Linear,
            Logarithmic,
        };

        public FreqScaleType FreqScale { get; set; }

        public enum FreqRangeType {
            SF_10HzTo100kHz,
            SF_0_1HzTo10Hz,
        };

        public FreqRangeType FreqRange { get; set; }

        public enum MagScaleType {
            Linear,
            Logarithmic,
        };

        public MagScaleType MagnitudeScale { get; set; }

        public enum MagnitudeRangeType {
            M96dB,
            M1dB,
            M3dB,
            M12dB,
            M48dB
        }

        public MagnitudeRangeType MagnitudeRange { get; set; }

        public bool ShowGain { get; set; }
        public bool ShowPhase { get; set; }

        /// <summary>
        /// Magnitude Scale(対数軸)の4乗根を戻す。
        /// </summary>
        private double MagnitudeRangeValue() {
            double[] mRange = new double[] {
                0.06309573444801932494343601366223, // -96dBの4乗根
                0.97162795157710617416734286616884, // -1dBの4乗根
                0.91727593538977958470087508027281, // -3dBの4乗根
                0.70794578438413791080221494218931, // -12dBの4乗根
                0.25118864315095801110850320677993, // -48dBの4乗根
            };

            return mRange[(int)MagnitudeRange];
        }


        public double PhaseShiftDegree { get; set; }

        private List<Line> mLineList = new List<Line>();
        private List<Label> mLabelList = new List<Label>();

        private Tuple<double, double> FreqStartEnd() {
            switch (FreqScale) {
            case FreqScaleType.Logarithmic:
                // 対数の時、開始周波数は0.1, 1, 10, 100, …でなければならない
                switch (FreqRange) {
                case FreqRangeType.SF_10HzTo100kHz:
                    return new Tuple<double, double>(10, 100 * 1000);
                case FreqRangeType.SF_0_1HzTo10Hz:
                    return new Tuple<double, double>(0.1, 10);
                }
                System.Diagnostics.Debug.Assert(false);
                break;
            case FreqScaleType.Linear:
                return new Tuple<double, double>(0, 100 * 1000);
            }

            System.Diagnostics.Debug.Assert(false);
            return new Tuple<double, double>(0, 100 * 1000);
        }

        /// <summary>
        /// プロット座標x → 周波数
        /// </summary>
        /// <param name="idx">0 <= idx < FR_LINE_WIDTH</param>
        /// <returns>周波数 Hz</returns>
        private double PlotXToFrequency(int idx) {
            switch (FreqScale) {
            case FreqScaleType.Linear:
                return FreqStartEnd().Item2 * idx / FR_LINE_WIDTH;
            case FreqScaleType.Logarithmic: {
                    var se = FreqStartEnd();

                    double startLog = Math.Log10(se.Item1);
                    double endLog = Math.Log10(se.Item2);
                    double freqLog = startLog + (endLog - startLog) * idx / FR_LINE_WIDTH;
                    return Math.Pow(10, freqLog);
                }
            }

            System.Diagnostics.Debug.Assert(false);
            return 0;
        }

        /// <summary>
        /// 周波数 → プロット座標x
        /// </summary>
        /// <param name="freq">周波数 Hz</param>
        /// <returns>プロット座標x</returns>
        private double FrequencyToPlotX(double freq) {
            switch (FreqScale) {
            case FreqScaleType.Linear:
                return (freq / FreqStartEnd().Item2) * FR_LINE_WIDTH;
            case FreqScaleType.Logarithmic: {
                    var se = FreqStartEnd();
                    double startLog = Math.Log10(se.Item1);
                    double endLog = Math.Log10(se.Item2);
                    double freqLog = Math.Log10(freq);

                    return ((freqLog - startLog) / (endLog - startLog)) * FR_LINE_WIDTH;
                }
            }

            System.Diagnostics.Debug.Assert(false);
            return 0;
        }

        private List<double> GenerateVGridFreqListLinear() {
            var result = new List<double>();
            double count = 10;
            double interval = FreqStartEnd().Item2 / count;

            double freq = 0;
            for (int i = 0; i < count; ++i) {
                result.Add(freq);
                freq += interval;
            }

            return result;
        }

        private List<double> GenerateVGridFreqListLogarithmic() {
            var result = new List<double>();
            var itemInDecade = new double[] { 1, 2, 5 };
            var freqStartEnd = FreqStartEnd();
            double freq = freqStartEnd.Item1;

            do {
                foreach (var item in itemInDecade) {
                    var f = freq;
                    f *= item;

                    if (freqStartEnd.Item2 < f) {
                        break;
                    }
                    result.Add(f);
                }

                freq *= 10;
            } while (freq <= freqStartEnd.Item2);

            return result;
        }

        /// <summary>
        /// グリッド縦棒の周波数のリストを生成。
        /// </summary>
        /// <returns></returns>
        private List<double> GenerateVerticalGridFreqList() {
            switch (FreqScale) {
            case FreqScaleType.Linear:
                return GenerateVGridFreqListLinear();
            case FreqScaleType.Logarithmic:
                return GenerateVGridFreqListLogarithmic();
            }

            System.Diagnostics.Debug.Assert(false);
            return null;
        }

        static private string FreqString(double freq) {
            if (freq < 1000) {
                return string.Format("{0}", freq);
            }
            if (freq < 1000 * 10) {
                return string.Format("{0:0.00}k", freq / 1000);
            }
            if (freq < 1000 * 100) {
                if (freq / 100 == (int)(freq / 100)) {
                    // 10.00kとか20.00kは10k,20kと書く
                    return string.Format("{0:0}k", freq / 1000);
                }
                return string.Format("{0:0.0}k", freq / 1000);
            }
            if (freq < 1000 * 1000) {
                return string.Format("{0:0}k", freq / 1000);
            }
            if (freq < 1000 * 1000 * 10) {
                if (freq / 10000 == (int)(freq / 10000)) {
                    // 1.00Mとか2.00Mは1M,2Mと書く
                    return string.Format("{0:0}M", freq / 1000 / 1000);
                }
                return string.Format("{0:0.00}M", freq / 1000 / 1000);
            }
            if (freq < 1000 * 1000 * 100) {
                if (freq / 100000 == (int)(freq / 100000)) {
                    // 10.0Mとか20.0Mは10M,20Mと書く
                    return string.Format("{0:0}M", freq / 1000 / 1000);
                }
                return string.Format("{0:0.0}M", freq / 1000 / 1000);
            }
            if (freq < 1000 * 1000 * 1000) {
                return string.Format("{0:0}M", freq / 1000 / 1000);
            }

            return string.Format("{0.00}G", freq / 1000 / 1000 / 1000);
        }

        private void LineSetPos(Line l, double x1, double y1, double x2, double y2) {
            l.X1 = x1;
            l.Y1 = y1;
            l.X2 = x2;
            l.Y2 = y2;
        }

        public delegate WWComplex TransferFunctionDelegate(WWComplex s);

        public TransferFunctionDelegate TransferFunction = (WWComplex s) => { return new WWComplex(1, 0); };

        public void Update() {
            foreach (var item in mLineList) {
                canvasFR.Children.Remove(item);
            }
            mLineList.Clear();

            foreach (var item in mLabelList) {
                canvasFR.Children.Remove(item);
            }
            mLabelList.Clear();
            
            // F特の計算。

            var fr = new WWComplex[FR_LINE_WIDTH];
            double maxMagnitude = 0.0f;

            for (int i = 0; i < FR_LINE_WIDTH; ++i) {
                double ω = 2.0 * Math.PI * PlotXToFrequency(i);
                var h = TransferFunction(new WWComplex(0, ω));
                double magnitude = h.Magnitude();
                if (maxMagnitude < magnitude) {
                    maxMagnitude = magnitude;
                }

                fr[i] = h;

                //Console.WriteLine("{0}Hz: {1}dB", ω / (2.0 * Math.PI), 20.0 * Math.Log10(h.Magnitude()));
            }

            if (maxMagnitude < float.Epsilon) {
                maxMagnitude = 1.0f;
            }

            labelPhase180.Content = string.Format("{0}", 180 + PhaseShiftDegree);
            labelPhase90.Content = string.Format("{0}", 90 + PhaseShiftDegree);
            labelPhase0.Content = string.Format("{0}", 0 + PhaseShiftDegree);
            labelPhaseM90.Content = string.Format("{0}", -90 + PhaseShiftDegree);
            labelPhaseM180.Content = string.Format("{0}", -180 + PhaseShiftDegree);

            var vGridFreqList = GenerateVerticalGridFreqList();
            foreach (var freq in vGridFreqList) {
                double x = FrequencyToPlotX(freq);

                {
                    var l = new Line();
                    LineSetPos(l, FR_LINE_LEFT + x, FR_LINE_TOP, FR_LINE_LEFT + x, FR_LINE_BOTTOM);
                    l.Stroke = new SolidColorBrush(Colors.LightGray);
                    canvasFR.Children.Add(l);
                    mLineList.Add(l);
                }

                {
                    var t = new Label();
                    t.Content = FreqString(freq);
                    canvasFR.Children.Add(t);
                    Canvas.SetLeft(t, FR_LINE_LEFT + x - 10);
                    Canvas.SetTop(t, FR_LINE_BOTTOM);
                    mLabelList.Add(t);
                }
            }

            double magRange = MagnitudeRangeValue();

            switch (MagnitudeScale) {
            case MagScaleType.Linear:
                labelMagnitude.Content = "Magnitude";
                labelFRMagMax.Content = string.Format("{0:0.00}", maxMagnitude);
                labelFRMag2.Content = string.Format("{0:0.00}", maxMagnitude * 0.75);
                labelFRMag1.Content = string.Format("{0:0.00}", maxMagnitude * 0.5);
                labelFRMag0.Content = string.Format("{0:0.00}", maxMagnitude * 0.25);
                labelFRMagMin.Content = string.Format("{0:0.00}", 0);

                lineFRMag0.Y1 = lineFRMag0.Y2 = FR_LINE_BOTTOM - FR_LINE_HEIGHT * 1 / 4;
                lineFRMag1.Y1 = lineFRMag1.Y2 = FR_LINE_BOTTOM - FR_LINE_HEIGHT * 2 / 4;
                lineFRMag2.Y1 = lineFRMag2.Y2 = FR_LINE_BOTTOM - FR_LINE_HEIGHT * 3 / 4;
                break;
            case MagScaleType.Logarithmic:
                labelMagnitude.Content = "Magnitude (dB)";
                labelFRMagMax.Content = string.Format("{0:0.00}", 20.0 * Math.Log10(maxMagnitude));
                labelFRMag2.Content = string.Format("{0:0.00}", 20.0 * Math.Log10(maxMagnitude * magRange));
                labelFRMag1.Content = string.Format("{0:0.00}", 20.0 * Math.Log10(maxMagnitude * magRange * magRange));
                labelFRMag0.Content = string.Format("{0:0.00}", 20.0 * Math.Log10(maxMagnitude * magRange * magRange * magRange));
                labelFRMagMin.Content = string.Format("{0:0.00}", 20.0 * Math.Log10(maxMagnitude * magRange * magRange * magRange * magRange));

                lineFRMag0.Y1 = lineFRMag0.Y2 = FR_LINE_BOTTOM - FR_LINE_HEIGHT * 1 / 4;
                lineFRMag1.Y1 = lineFRMag1.Y2 = FR_LINE_BOTTOM - FR_LINE_HEIGHT * 2 / 4;
                lineFRMag2.Y1 = lineFRMag2.Y2 = FR_LINE_BOTTOM - FR_LINE_HEIGHT * 3 / 4;
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }

            {
                var visibility = ShowGain ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
                labelFRMag0.Visibility = visibility;
                labelFRMag1.Visibility = visibility;
                labelFRMag2.Visibility = visibility;
                labelFRMagMax.Visibility = visibility;
                labelFRMagMin.Visibility = visibility;
                labelMagnitude.Visibility = visibility;
            }

            {
                var visibility = ShowPhase ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
                labelPhase.Visibility = visibility;
                labelPhase0.Visibility = visibility;
                labelPhase90.Visibility = visibility;
                labelPhase180.Visibility = visibility;
                labelPhaseM180.Visibility = visibility;
                labelPhaseM90.Visibility = visibility;
            }

            // 周波数応答の折れ線を作る。

            var lastPosM = new Point();
            var lastPosP = new Point();

            for (int i = 0; i < FR_LINE_WIDTH; ++i) {
                Point posM = new Point();
                Point posP = new Point();

                double phase = fr[i].Phase() + PhaseShiftDegree;
                while (phase <= -Math.PI) {
                    phase += 2.0 * Math.PI;
                }
                while (Math.PI < phase) {
                    phase -= 2.0f * Math.PI;
                }

                posP = new Point(FR_LINE_LEFT + i, FR_LINE_YCENTER - FR_LINE_HEIGHT * phase / (2.0f * Math.PI));

                switch (MagnitudeScale) {
                case MagScaleType.Linear:
                    posM = new Point(FR_LINE_LEFT + i, FR_LINE_BOTTOM - FR_LINE_HEIGHT * fr[i].Magnitude() / maxMagnitude);
                    break;
                case MagScaleType.Logarithmic:
                    posM = new Point(FR_LINE_LEFT + i,
                        FR_LINE_TOP 
                        + FR_LINE_HEIGHT * 20.0 * Math.Log10(fr[i].Magnitude() / maxMagnitude)
                          / (20.0 * Math.Log10(magRange * magRange * magRange * magRange)));
                    break;
                default:
                    System.Diagnostics.Debug.Assert(false);
                    break;
                }

                if (1 <= i) {
                    bool bDraw = true;
                    switch (MagnitudeScale) {
                    case MagScaleType.Logarithmic:
                        if (FR_LINE_BOTTOM < posM.Y || FR_LINE_BOTTOM < lastPosM.Y) {
                            bDraw = false;
                        }
                        break;
                    }


                    if (bDraw && ShowGain) {
                        var lineM = new Line();
                        lineM.Stroke = Brushes.Blue;
                        LineSetPos(lineM, lastPosM.X, lastPosM.Y, posM.X, posM.Y);
                        mLineList.Add(lineM);
                        canvasFR.Children.Add(lineM);
                    }
                }

                if (2 <= i && ShowPhase) {
                    var lineP = new Line();
                    lineP.Stroke = Brushes.Red;
                    LineSetPos(lineP, lastPosP.X, lastPosP.Y, posP.X, posP.Y);
                    mLineList.Add(lineP);
                    canvasFR.Children.Add(lineP);
                }
                lastPosP = posP;
                lastPosM = posM;
            }
        }

    }
}
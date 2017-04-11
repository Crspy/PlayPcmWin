﻿using System;
using System.Collections.Generic;
using WWIIRFilterDesign;
using WWMath;

namespace WWOfflineResampler {
    public class IIRFilterDesign {
        public enum Method {
            ImpulseInvarianceMinimumPhase,
            ImpulseInvarianceMixedPhase,
            Bilinear,
        };

        private Method mMethod;

        private const double CUTOFF_GAIN_DB = -1.0;

        public long SamplingFrequency { get; set; }

        // Impulse invariance methodのとき
        // -10 : 3次 ◎ (poles 対称)(zeroes 虚数1ペア)
        // -20 : 5次 (4次) ×
        // -30 : 5次 ◎ (poles 対称)
        // -40 : 7次 (6次) ×
        // -50 : 7次 ◎ (poles 対称) 
        // -60 : 9次 (8次) ×
        // -70 : 9次 〇 (poles 対称)(zeroesすべて実数)
        // -80 : 11次 (10次) ×
        // -90 : 11次 〇 (poles 対称)(zeroes 虚数1ペア)
        // -100 : 13次 (12次) ×
        // -110 : 13次 〇 (poles 対称)(zeroesすべて実数)
        // -120 : 15次 (14次) ×
        private double StopbandRippleDb() {
            switch (mMethod) {
            case Method.ImpulseInvarianceMinimumPhase:
                return -50;
            case Method.ImpulseInvarianceMixedPhase:
                return -110;
            case Method.Bilinear:
                return -110;
            default:
                return -110;
            }
        }

        private WWAnalogFilterDesign.AnalogFilterDesign mAfd;
        private WWIIRFilterDesign.ImpulseInvarianceMethod mIIRiim;
        private WWIIRFilterDesign.BilinearDesign mIIRBilinear;

        public WWAnalogFilterDesign.AnalogFilterDesign Afd() {
            return mAfd;
        }

        public WWIIRFilterDesign.ImpulseInvarianceMethod IIRiim() {
            return mIIRiim;
        }

        public WWMath.Functions.TransferFunctionDelegate TransferFunction() {
            switch (mMethod) {
            case Method.Bilinear:
                return mIIRBilinear.TransferFunction;
            default:
                return mIIRiim.TransferFunction;
            }
        }

        public IIRFilterGraph CreateIIRFilterGraph() {
            IIRFilterGraph iirFilter = null;

            // ローパスフィルターを作る。
            // 実数係数版の多項式を使用。
            switch (mMethod) {
            case Method.ImpulseInvarianceMinimumPhase:
                iirFilter = new IIRFilterSerial();
                break;
            case Method.ImpulseInvarianceMixedPhase:
            case Method.Bilinear:
                iirFilter = new IIRFilterParallel();
                break;
            }

            if (mMethod == Method.Bilinear) {
                for (int i = 0; i < mIIRBilinear.RealHzCount(); ++i) {
                    RealRationalPolynomial p = mIIRBilinear.RealHz(i);
                    Console.WriteLine("{0}", p.ToString("(z)^(-1)"));
                    iirFilter.Add(p);
                }
            } else {
                for (int i = 0; i < mIIRiim.RealHzCount(); ++i) {
                    RealRationalPolynomial p = mIIRiim.RealHz(i);
                    Console.WriteLine("{0}", p.ToString("(z)^(-1)"));
                    iirFilter.Add(p);
                }
            }
            return iirFilter;
        }

        public bool Design(double fc, double fs, long samplingFreq, Method method) {
            mMethod = method;
            SamplingFrequency = samplingFreq;

            switch (method) {
            case Method.Bilinear:
                return DesignBilinear(fc,fs,samplingFreq);
            default:
                return DesignImpulseInvariance(fc, fs, samplingFreq);
            }
        }

        private bool DesignImpulseInvariance(double fc, double fs, long samplingFreq) {

            mAfd = new WWAnalogFilterDesign.AnalogFilterDesign();
            mAfd.DesignLowpass(0, CUTOFF_GAIN_DB, StopbandRippleDb(),
                fc,
                fs,
                WWAnalogFilterDesign.AnalogFilterDesign.FilterType.Cauer,
                WWAnalogFilterDesign.ApproximationBase.BetaType.BetaMax);

            var H_s = new List<FirstOrderComplexRationalPolynomial>();
            for (int i = 0; i < mAfd.HPfdCount(); ++i) {
                var p = mAfd.HPfdNth(i);
                H_s.Add(p);
            }

            mIIRiim = new ImpulseInvarianceMethod(H_s, fc * 2.0 * Math.PI, samplingFreq, mMethod == Method.ImpulseInvarianceMinimumPhase);
            return true;
        }

        private bool DesignBilinear(double fc, double fs, long samplingFreq) {
            mIIRBilinear = new BilinearDesign();

            double fc_s = mIIRBilinear.PrewarpωtoΩ(2.0 * Math.PI * fc / samplingFreq);
            double fs_s = mIIRBilinear.PrewarpωtoΩ(2.0 * Math.PI * fs / samplingFreq);

            mAfd = new WWAnalogFilterDesign.AnalogFilterDesign();
            mAfd.DesignLowpass(0, CUTOFF_GAIN_DB, StopbandRippleDb(),
                fc_s, fs_s,
                WWAnalogFilterDesign.AnalogFilterDesign.FilterType.Cauer,
                WWAnalogFilterDesign.ApproximationBase.BetaType.BetaMax);

            // 連続時間伝達関数を離散時間伝達関数に変換。
            for (int i = 0; i < mAfd.HPfdCount(); ++i) {
                var s = mAfd.HPfdNth(i);
                mIIRBilinear.Add(s);
            }
            mIIRBilinear.Calc();

            return true;
        }

        public int NumOfPoles() {
            return mAfd.NumOfPoles();
        }

        public WWComplex PoleNth(int nth) {
            switch (mMethod) {
            case Method.Bilinear:
                return mIIRBilinear.StoZ(mAfd.PoleNth(nth)).Reciplocal();
            default:
                return mIIRiim.PoleNth(nth);
            }
        }

        public int NumOfZeroes() {
            switch (mMethod) {
            case Method.Bilinear:
                return mAfd.NumOfZeroes();
            default:
                return mIIRiim.NumOfZeroes();
            }
        }

        public WWComplex ZeroNth(int nth) {
            switch (mMethod) {
            case Method.Bilinear:
                return mIIRBilinear.StoZ(mAfd.ZeroNth(nth)).Reciplocal();
            default:
                return mIIRiim.ZeroNth(nth);
            }
        }
    }
}

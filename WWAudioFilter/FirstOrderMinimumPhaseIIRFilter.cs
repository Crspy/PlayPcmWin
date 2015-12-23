﻿using System;
using System.Globalization;

namespace WWAudioFilter {
    public class FirstOrderMinimumPhaseIIRFilter : FilterBase {
        public double K { get; set; }
        private double mLastX;
        private double mLastY;

        public FirstOrderMinimumPhaseIIRFilter(double k)
                : base(FilterType.FirstOrderMinimumPhaseIIR) {
            if (k < 0) {
                throw new ArgumentOutOfRangeException("k");
            }

            K = k;
        }

        public override FilterBase CreateCopy() {
            return new FirstOrderMinimumPhaseIIRFilter(K);
        }

        public override string ToDescriptionText() {
            return string.Format(CultureInfo.CurrentCulture, Properties.Resources.FilterFirstOrderMinimumPhaseIIRDesc,
                K);
        }

        public override string ToSaveText() {
            return string.Format(CultureInfo.InvariantCulture, "{0}", K);
        }

        public static FilterBase Restore(string[] tokens) {
            if (tokens.Length != 2) {
                return null;
            }

            double k;
            if (!Double.TryParse(tokens[1], out k) || k <= Double.Epsilon) {
                return null;
            }

            return new FirstOrderMinimumPhaseIIRFilter(k);
        }

        public override void FilterStart() {
            mLastX = 0;
            mLastY = 0;
            base.FilterStart();
        }

        public override void FilterEnd() {
            base.FilterEnd();
        }

        /*
         * Transfer function H(z):
         *             -k + z^{-1}
         *   H(z) = ------------------
         *            1 - k * z^{-1}
         *
         * Differential equation:
         * Input:  x[n]
         * Output: y[n]
         * 
         * y[n] = -k * x[n] + x[n-1] - k * y[n-1]
         */
        public override double[] FilterDo(double[] inPcm) {
            var outPcm = new double[inPcm.Length];

            for (int i=0; i < inPcm.Length; ++i) {
                double x = inPcm[i];

                double y = -K * x + mLastX;
                y += -K * mLastY;

                outPcm[i] = y;

                mLastX = x;
                mLastY = y;
            }

            return outPcm;
        }
    }
}
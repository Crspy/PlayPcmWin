﻿using System;
using System.Collections.Generic;
using WWMath;

namespace WWIIRFilterDesign {
    public class BilinearDesign {
        private List<FirstOrderComplexRationalPolynomial> mH_s = new List<FirstOrderComplexRationalPolynomial>();
        private List<FirstOrderComplexRationalPolynomial> mComplexHzList = new List<FirstOrderComplexRationalPolynomial>();
        private List<RealRationalPolynomial> mRealHzList = new List<RealRationalPolynomial>();
        private RealRationalPolynomial mHzCombined;
        private double mMatchFreq;
        private double mSampleFreq;

        public enum FilterType {
            Lowpass,
            Highpass,
        };

        private FilterType mFilterType = FilterType.Lowpass;

        /// <summary>
        /// バイリニア変換でIIRフィルターを設計する。
        /// </summary>
        /// <param name="matchFreq">アナログフィルターとデジタルフィルターのゲインが一致する周波数(Hz)</param>
        /// <param name="sampleFreq">サンプリング周波数 (Hz)</param>
        public BilinearDesign(double matchFreq, double sampleFreq) {
            mMatchFreq = matchFreq;
            mSampleFreq = sampleFreq;
        }

        public int RealHzCount() {
            return mRealHzList.Count;
        }

        public RealRationalPolynomial RealHz(int nth) {
            return mRealHzList[nth];
        }

        public double Td {
            get { return 1.0 / mSampleFreq; }
        }

        public int HsNum() {
            return mH_s.Count;
        }

        public FirstOrderComplexRationalPolynomial HsNth(int nth) {
            return mH_s[nth];
        }

        /// <summary>
        /// 伝達関数を構成する1次複素分数関数の数を戻す。伝達関数はこの分数関数の和。
        /// </summary>
        public int HzNum() {
            return mComplexHzList.Count;
        }

        public FirstOrderComplexRationalPolynomial HzNth(int nth) {
            return mComplexHzList[nth];
        }

        /// <summary>
        /// 伝達関数を1個の分数関数に合体したものを戻す。
        /// </summary>
        public RealRationalPolynomial HzCombined() {
            return mHzCombined;
        }

        public WWMath.Functions.TransferFunctionDelegate TransferFunction;

        /// <summary>
        /// 離散時間角周波数ωを連続時間角周波数Ωにprewarpする。
        /// Discrete-time signal processing 3rd ed. pp.534, eq 7.26
        /// 三谷政昭, ディジタル・フィルタ理論&設計入門 pp.193
        /// </summary>
        /// <param name="ω">離散時間角周波数ω (π==ナイキスト周波数)</param>
        /// <returns>Ω (rad/s)</returns>
        public double PrewarpωtoΩ(double ω) {
            double Ω = 2.0 / Td * Math.Tan(ω * Td / 2.0);
            return Ω;
        }

        /// <summary>
        /// アナログフィルターの零や極の座標をバイリニア変換する。
        /// Discrete-time signal processing 3rd ed. pp534 eq7.21
        /// 三谷政昭, ディジタル・フィルタ理論&設計入門 pp.193
        /// </summary>
        /// <param name="s">s平面上の座標s'。アナログフィルターのs'は、s' == s / ωcとしたs'についての式になっている</param>
        /// <returns>IIRフィルターのZ平面上の座標</returns>
        public WWComplex StoZ(WWComplex s) {
            double twoπ = 2.0 * Math.PI;
            double ωc = PrewarpωtoΩ(mMatchFreq * twoπ);
            return StoZ(s, ωc);
        }

        /// <summary>
        /// アナログフィルターの零や極の座標をバイリニア変換する。
        /// Discrete-time signal processing 3rd ed. pp534 eq7.21
        /// 三谷政昭, ディジタル・フィルタ理論&設計入門 pp.193
        /// </summary>
        /// <param name="s">s平面上の座標s'。アナログフィルターのs'は、s' == s / ωcとしたs'についての式になっている</param>
        /// <param name="ωc">マッチ周波数(rad)。アナログフィルターとIIRフィルターの特性が一致する周波数。</param>
        /// <returns>IIRフィルターのZ平面上の座標</returns>
        public WWComplex StoZ(WWComplex s, double ωc) {
            WWComplex s2 = s.Scale(ωc * Td / 2.0);
            return WWComplex.Div(
                new WWComplex(1.0 + s2.real, s2.imaginary),
                new WWComplex(1.0 - s2.real, -s2.imaginary)
                );
        }

        /// <summary>
        /// 連続時間フィルターの伝達関数を離散時間フィルターの伝達関数にBilinear transformする。
        /// Discrete-time signal processing 3rd ed. pp.533
        /// Benoit Boulet, 信号処理とシステムの基礎 pp.681-682
        /// 三谷政昭, ディジタル・フィルタ理論&設計入門 pp.193
        /// </summary>
        /// <param name="ps">連続時間フィルターの伝達関数</param>
        /// <returns>離散時間フィルターの伝達関数</returns>
        public FirstOrderComplexRationalPolynomial StoZ(FirstOrderComplexRationalPolynomial ps) {
            /*
                   n1s + n0
             ps = ──────────
                   d1s + d0
             
             z^{-1} = zM とする。
             
             Bilinear transform:
                  2     1-zM
             s → ─── * ──────
                  Td    1+zM
             
             2/Td = kとすると以下のように書ける。
             
                 k(1-zM)
             s → ───────
                  1+zM
             
                        k(1-zM)          n1*k(1-zM) + n0(1+zM)
                   n1 * ─────── + n0     ─────────────────────
                         1+zM                    1+zM              n1*k(1-zM) + n0(1+zM)   (n0-n1*k)zM + n0+n1*k
             pz = ─────────────────── = ──────────────────────── = ───────────────────── = ─────────────────────
                        k(1-zM)          d1*k(1-zM) + d0(1+zM)     d1*k(1-zM) + d0(1+zM)   (d0-d1*k)zM + d0+d1*k
                   d1 * ─────── + d0     ─────────────────────
                         1+zM                    1+zM
             
             */

            // 都合により、投入されるアナログフィルターの伝達関数psは、
            // s' == s / ωcとしたs'についての式であることに注意!!

            double twoπ = 2.0 * Math.PI;
            double ωc = PrewarpωtoΩ(mMatchFreq * twoπ);
            double k = (1.0 / ωc) * (2.0 * mSampleFreq);

            var n0  = ps.N(0);
            var n1k = WWComplex.Mul(ps.N(1), k);
            var d0  = ps.D(0);
            var d1k = WWComplex.Mul(ps.D(1), k);

            var pz = new FirstOrderComplexRationalPolynomial(
                WWComplex.Sub(n0, n1k), WWComplex.Add(n0, n1k),
                WWComplex.Sub(d0, d1k), WWComplex.Add(d0, d1k));

            return pz;
        }

        /// <summary>
        /// アナログフィルターの伝達関数psをBilinear変換器に投入する。
        /// </summary>
        public void Add(FirstOrderComplexRationalPolynomial ps) {
            mH_s.Add(ps);
            mComplexHzList.Add(StoZ(ps));
        }

        /// <summary>
        /// ローパス→ハイパス変換。
        /// 全てAdd()後、Calc()の前に呼ぶ。
        /// mComplexHzList[]がハイパスに変換される。
        /// </summary>
        public void LowpassToHighpass() {
            for (int i = 0; i < mComplexHzList.Count; ++i) {
                var p = mComplexHzList[i];
                var r = Transformations.LowpassToHighpass(p);
                mComplexHzList[i] = r;
            }

            if (mFilterType == FilterType.Lowpass) {
                mFilterType = FilterType.Highpass;
            } else {
                mFilterType = FilterType.Lowpass;
            }
        }

        /// <summary>
        /// Addし終わったら呼ぶ。
        /// </summary>
        public void Calc() {
            // mComplexHzListに1次の関数が入っている。

            //　係数が全て実数のmRealHzListを作成する。
            // mRealHzListは、多項式の和を表現する。
            mRealHzList.Clear();
            for (int i = 0; i < mComplexHzList.Count / 2; ++i) {
                var p0 = mComplexHzList[i];
                var p1 = mComplexHzList[mComplexHzList.Count - 1 - i];
                var p = WWPolynomial.Add(p0, p1).ToRealPolynomial();
                mRealHzList.Add(p);
            }

            if ((mComplexHzList.Count & 1) == 1) {
                // 1次の項。
                var p = mComplexHzList[mComplexHzList.Count / 2];
                mRealHzList.Add(new RealRationalPolynomial(
                    new double[] { p.N(0).real, p.N(1).real },
                    new double[] { p.D(0).real, p.D(1).real }));
            }

            var stopbandGain = WWComplex.Zero();

            if (mFilterType == FilterType.Lowpass) {
                foreach (var p in mRealHzList) {
                    stopbandGain = WWComplex.Add(stopbandGain, p.Evaluate(WWComplex.Unity()));
                }
            } else {
                foreach (var p in mRealHzList) {
                    stopbandGain = WWComplex.Add(stopbandGain, p.Evaluate(new WWComplex(-1, 0)));
                }
            }

            // DCゲインが1になるようにHzをスケールする。
            for (int i=0; i<mRealHzList.Count; ++i) {
                var p = mRealHzList[i];
                mRealHzList[i] = p.ScaleNumerCoeffs(1.0 / stopbandGain.real);
            }

            stopbandGain = WWComplex.Zero();
            foreach (var p in mRealHzList) {
                stopbandGain = WWComplex.Add(stopbandGain, p.Evaluate(WWComplex.Unity()));
            }
            Console.WriteLine("DC gain={0}", stopbandGain);

            TransferFunction = (WWComplex z) => { return TransferFunctionValue(z); };

            mHzCombined = new RealRationalPolynomial(mRealHzList[0]);
            for (int i = 1; i < mRealHzList.Count; ++i) {
                var p = mRealHzList[i];
                mHzCombined = WWPolynomial.Add(mHzCombined, p);
            }
            mHzCombined = mHzCombined.ScaleAllCoeffs(1.0 / mHzCombined.D(0));
        }

        private WWComplex TransferFunctionValue(WWComplex z) {
            var zRecip = WWComplex.Reciprocal(z);
#if false
            // 1次有理多項式の和の形の式で計算。
            var result = WWComplex.Zero();
            foreach (var H in mComplexHzList) {
                result = WWComplex.Add(result, H.Evaluate(zRecip));
            }
            return result;
#endif
#if true
            // 2次有理多項式の和の形の式で計算。
            var result = WWComplex.Zero();
            foreach (var H in mRealHzList) {
                result = WWComplex.Add(result, H.Evaluate(zRecip));
            }
            return result;
#endif
#if false
            // アナログフィルターの伝達関数を使用。
            var s = ZtoS(z);
            var result = WWComplex.Zero();
            foreach (var H in mH_s) {
                result = WWComplex.Add(result, H.Evaluate(s));
            }
            return result;
#endif
        }
    }
}

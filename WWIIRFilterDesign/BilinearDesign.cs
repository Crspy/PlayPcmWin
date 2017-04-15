﻿using System;
using System.Collections.Generic;
using WWMath;

namespace WWIIRFilterDesign {
    public class BilinearDesign {
        private List<RealRationalPolynomial> mRealHzList = new List<RealRationalPolynomial>();

        public int RealHzCount() {
            return mRealHzList.Count;
        }

        public RealRationalPolynomial RealHz(int nth) {
            return mRealHzList[nth];
        }

        private double mTd;
        private double mMatchFreq;
        private double mSampleFreq;

        public double Td {
            get { return mTd; }
        }

        public BilinearDesign(double td, double matchFreq, double sampleFreq) {
            mTd = td;
            mMatchFreq = matchFreq;
            mSampleFreq = sampleFreq;
        }

        private List<FirstOrderComplexRationalPolynomial> mH_s = new List<FirstOrderComplexRationalPolynomial>();
        private List<FirstOrderComplexRationalPolynomial> mComplexHzList = new List<FirstOrderComplexRationalPolynomial>();

        public int HsNum() {
            return mH_s.Count;
        }

        public FirstOrderComplexRationalPolynomial HsNth(int nth) {
            return mH_s[nth];
        }

        public int HzNum() {
            return mComplexHzList.Count;
        }

        public FirstOrderComplexRationalPolynomial HzNth(int nth) {
            return mComplexHzList[nth];
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
        /// Discrete-time signal processing 3rd ed. pp534 eq7.21
        /// 三谷政昭, ディジタル・フィルタ理論&設計入門 pp.193
        /// </summary>
        public WWComplex StoZ(WWComplex s) {
            // アナログフィルターのs'は、s' == s / ωcとしたs'についての式になっている

            double ωc = 2.0 * Math.PI * mMatchFreq;
            WWComplex s2 = s.Scale(ωc * Td/2.0);
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

            // アナログフィルターのpsは、s' == s / ωcとしたs'についての式になっている

            double ωc = 2.0 * Math.PI * mMatchFreq;
            double k = (1.0/ωc) * (2.0 / Td);
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
        /// Addし終わったら呼ぶ。
        /// </summary>
        public void Calc() {
            // mH_zに1次の関数が入っている。

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
                var p = mComplexHzList[mComplexHzList.Count / 2];

                mRealHzList.Add(new RealRationalPolynomial(
                    new double[] { p.N(0).real, p.N(1).real },
                    new double[] { p.D(0).real, p.D(1).real }));
            }

            var gainR = 0.0;
            foreach (var p in mRealHzList) {
                gainR += p.Evaluate(1.0);
            }
            Console.WriteLine("gainR={0}", gainR);

            TransferFunction = (WWComplex z) => { return TransferFunctionValue(z); };
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

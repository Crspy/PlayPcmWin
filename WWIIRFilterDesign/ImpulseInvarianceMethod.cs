﻿using System;
using System.Collections.Generic;
using WWMath;

namespace WWIIRFilterDesign {
    public class ImpulseInvarianceMethod {

        // y[1] : z^{-2}の項
        // y[1] : z^{-1}の項
        // y[0] : 定数項
        private HighOrderComplexRationalPolynomial mH_z;
        public HighOrderComplexRationalPolynomial HzCombined() {
            return mH_z;
        }

        private List<RealRationalPolynomial> mRealHzList = new List<RealRationalPolynomial>();

        public int RealHzCount() {
            return mRealHzList.Count;
        }

        public RealRationalPolynomial RealHz(int nth) {
            return mRealHzList[nth];
        }

        private List<FirstOrderComplexRationalPolynomial> mComplexHzList = new List<FirstOrderComplexRationalPolynomial>();

        public int ComplexHzCount() {
            return mComplexHzList.Count;
        }

        public FirstOrderComplexRationalPolynomial ComplexHz(int nth) {
            return mComplexHzList[nth];
        }

        private double mSamplingFrequency;
        public double SamplingFrequency() {
            return mSamplingFrequency;
        }

        public WWMath.Functions.TransferFunctionDelegate TransferFunction;

        /// <summary>
        /// Design of Discrete-time IIR filters from continuous-time filter using impulse invariance method
        /// A. V. Oppenheim, R. W. Schafer, Discrete-Time Signal Processing, 3rd Ed, Prentice Hall, 2009
        /// pp. 526 - 529
        /// </summary>
        public ImpulseInvarianceMethod(List<FirstOrderComplexRationalPolynomial> H_s,
                double ωc, double sampleFreq) {
            mSamplingFrequency = sampleFreq;
            /*
             * H_sはノーマライズされているので、戻す。
             * 
             *     b          b * ωc
             * ────────── = ────────────
             *  s/ωc - a     s - a * ωc
             */

            double td = 1.0 / sampleFreq;

            mComplexHzList.Clear();
            foreach (var pS in H_s) {
                WWComplex sktd;
                if (pS.DenomDegree() == 0) {
                    System.Diagnostics.Debug.Assert(pS.D(0).EqualValue(WWComplex.Unity()));
                    // ? 
                    // a * u[t] → exp^(a)
                    sktd = WWComplex.Minus(WWComplex.Mul(WWComplex.Unity(), ωc * td));
                } else {
                    sktd = WWComplex.Minus(WWComplex.Mul(pS.D(0), ωc * td));
                }

                // e^{sktd} = e^{real(sktd)} * e^{imag{sktd}}
                //          = e^{real(sktd)} * ( cos(imag{sktd}) + i*sin(imag{sktd})
                var expsktd = new WWComplex(
                    Math.Exp(sktd.real) * Math.Cos(sktd.imaginary),
                    Math.Exp(sktd.real) * Math.Sin(sktd.imaginary));

                // pZは、z^-1についての式。
                // y[1] : z^{-1}の項
                // y[0] : 定数項
                var pZ = new FirstOrderComplexRationalPolynomial(
                    WWComplex.Zero(), WWComplex.Mul(pS.N(0), ωc * td),
                    WWComplex.Minus(expsktd), WWComplex.Unity());

                mComplexHzList.Add(pZ);
            }

            mH_z = new HighOrderComplexRationalPolynomial(mComplexHzList[0]);
            for (int i = 1; i < mComplexHzList.Count; ++i) {
                mH_z = WWPolynomial.Add(mH_z, mComplexHzList[i]);
            }

            // ミニマムフェーズにする。
            {
                var numerPoly = mH_z.NumerPolynomial();
                var aCoeffs = new double[numerPoly.Degree + 1];
                for (int i = 0; i < aCoeffs.Length; ++i) {
                    aCoeffs[i] = numerPoly.C(i).real;
                }

                var rpoly = new JenkinsTraubRpoly();
                bool result = rpoly.FindRoots(new RealPolynomial(aCoeffs));
                if (result) {
                    // ポールの位置 = mHzListの多項式の分母のリストから判明する。
                    var poles = new WWComplex[mComplexHzList.Count];
                    for (int i = 0; i < mComplexHzList.Count; ++i) {
                        var p = mComplexHzList[i];
                        Console.WriteLine(" {0} {1}", i, p);
                        poles[i] = WWComplex.Div(p.D(0), p.D(1)).Minus();
                    }

                    System.Diagnostics.Debug.Assert(poles.Length == rpoly.NumOfRoots() + 1);

                    var zeroes = new WWComplex[rpoly.NumOfRoots()];
                    for (int i = 0; i < rpoly.NumOfComplexRoots() / 2; ++i) {
                        var p0 = rpoly.ComplexRoot(i * 2);
                        var p1 = rpoly.ComplexRoot(i * 2 + 1);
                        if (p0.Magnitude() < 1.0) {
                            // 単位円の外側にゼロがあるのでconjugate reciprocalにする。
                            p0 = p0.ConjugateReciprocal();
                            p1 = p1.ConjugateReciprocal();
                        }
                        zeroes[i*2] = p0;
                        zeroes[i*2+1] = p1;
                    }
                    for (int i = 0; i < rpoly.NumOfRealRoots(); ++i) {
                        var p = rpoly.RealRoot(i);
                        if (p.Magnitude() < 1.0) {
                            // 単位円の外側にゼロがあるのでconjugate reciprocalにする。
                            p = p.ConjugateReciprocal();
                        }
                        zeroes[i + rpoly.NumOfComplexRoots()] = p;
                    }

                    mComplexHzList.Clear();

                    // ポールと零のペアを、係数を実数化出来るように対称に並べる。
                    // ポールと共役のペアになっていて、ペアは例えばn=5の時
                    // C0+ C1+ R C1- C0- のように並んでいる。
                    // 零は共役のペアになっていて、ペアは例えばn=5の時
                    // C0+ C0- C1+ C1- R のように並んでいる。
                    // mComplexHzListは C0+ C0- C1+ C1- R のように並べる。
                    for (int i = 0; i < poles.Length / 2; ++i) {
                        var cP = new FirstOrderComplexRationalPolynomial(
                            WWComplex.Unity(), zeroes[i*2].Minus(),
                            WWComplex.Unity(), poles[i].Minus());
                        mComplexHzList.Add(cP);
                        var cM = new FirstOrderComplexRationalPolynomial(
                            WWComplex.Unity(), zeroes[i * 2+1].Minus(),
                            WWComplex.Unity(), poles[poles.Length-1-i].Minus());
                        mComplexHzList.Add(cM);
                    }
                    {
                        var p = new FirstOrderComplexRationalPolynomial(
                            WWComplex.Zero(), WWComplex.Unity(),
                            WWComplex.Unity(), poles[poles.Length/2].Minus());
                        mComplexHzList.Add(p);
                    }

                    // 0Hz (z^-1 == 1)のときのゲインが1になるようにする。
                    WWComplex gain = WWComplex.Unity();
                    foreach (var p in mComplexHzList) {
                        gain = WWComplex.Mul(gain, p.Evaluate(WWComplex.Unity()));
                    }
                    mComplexHzList[mComplexHzList.Count-1] =
                        mComplexHzList[mComplexHzList.Count-1].Scale(1.0/gain.real);
                    
                    var gainC = WWComplex.Unity();
                    foreach (var p in mComplexHzList) {
                        gainC = WWComplex.Mul(gainC, p.Evaluate(WWComplex.Unity()));
                    }

                    //　係数が全て実数のmRealHzListを作成する。
                    mRealHzList.Clear();
                    for (int i = 0; i < mComplexHzList.Count / 2; ++i) {
                        var p0 = mComplexHzList[i*2];
                        var p1 = mComplexHzList[i*2+1];
                        var p = WWPolynomial.Mul(p0, p1).ToRealPolynomial();
                        mRealHzList.Add(p);
                    }
                    mRealHzList.Add(new RealRationalPolynomial(
                        new double[] { 1.0 / gain.real },
                        new double[] { -poles[poles.Length/2].real, 1.0 }));

                    var gainR = 1.0;
                    foreach (var p in mRealHzList) {
                        gainR *= p.Evaluate(1.0);
                    }

                    // mH_zを作り直す。
                    var newNumerCoeffs = WWPolynomial.RootListToCoeffList(zeroes, WWComplex.Unity());

                    var poleCoeffs = mH_z.DenomPolynomial().ToArray();
                    mH_z = new HighOrderComplexRationalPolynomial(newNumerCoeffs, poleCoeffs);

                    var gain2 = mH_z.Evaluate(WWComplex.Unity());
                    for (int i = 0; i < newNumerCoeffs.Length; ++i) {
                        newNumerCoeffs[i] = WWComplex.Mul(newNumerCoeffs[i], 1.0/gain2.Magnitude());
                    }

                    mH_z = new HighOrderComplexRationalPolynomial(newNumerCoeffs, poleCoeffs);
                    var gain3 = mH_z.Evaluate(WWComplex.Unity());

                    Console.WriteLine(mH_z.ToString("z", WWUtil.SymbolOrder.Inverted));
                }
            }

            TransferFunction = (WWComplex z) => { return TransferFunctionValue(z); };
        }

        private WWComplex TransferFunctionValue(WWComplex z) {
#if false
            // mComplexHzListは1次有理多項式の積の形になっている。
            var zRecip = WWComplex.Reciprocal(z);
            var result = WWComplex.Unity();
            foreach (var H in mComplexHzList) {
                result = WWComplex.Mul(result, H.Evaluate(zRecip));
            }
            return result;
#endif
#if true
            // 実係数多項式の積の形
            var zRecip = WWComplex.Reciprocal(z);
            var result = WWComplex.Unity();
            foreach (var H in mRealHzList) {
                result = WWComplex.Mul(result, H.Evaluate(zRecip));
            }
            return result;
#endif

#if false
            // mH_zは1個に合体した有理多項式で計算。

            var zN = WWComplex.Unity();
            var numer = WWComplex.Zero();
            for (int i = 0; i < mH_z.NumerDegree()+1; ++i) {
                numer = WWComplex.Add(numer, WWComplex.Mul(mH_z.N(i), zN));
                zN = WWComplex.Div(zN, z);
            }

            zN = WWComplex.Unity();
            var denom = WWComplex.Zero();
            for (int i = 0; i < mH_z.DenomDegree() + 1; ++i) {
                denom = WWComplex.Add(denom, WWComplex.Mul(mH_z.D(i), zN));
                zN = WWComplex.Div(zN, z);
            }
            return WWComplex.Div(numer, denom);
#endif
        }

    }
}

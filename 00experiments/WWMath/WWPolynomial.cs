﻿using System;
using System.Collections.Generic;

namespace WWMath {
    public class WWPolynomial {
        /// <summary>
        /// 1次有理多項式 x 1次有理多項式
        /// </summary>
        public static SecondOrderRationalPolynomial Mul(FirstOrderRationalPolynomial lhs, FirstOrderRationalPolynomial rhs) {
            // 分子の項 x 分子の項
            var n2 = WWComplex.Mul(lhs.NumeratorCoeff(1), rhs.NumeratorCoeff(1));
            var n1 = WWComplex.Add(WWComplex.Mul(lhs.NumeratorCoeff(1), rhs.NumeratorCoeff(0)),
                                   WWComplex.Mul(lhs.NumeratorCoeff(0), rhs.NumeratorCoeff(1)));
            var n0 = WWComplex.Mul(lhs.NumeratorCoeff(0), rhs.NumeratorCoeff(0));

            // 分母の項 x 分母の項
            var d2 = WWComplex.Mul(lhs.DenominatorCoeff(1), rhs.DenominatorCoeff(1));
            var d1 = WWComplex.Add(WWComplex.Mul(lhs.DenominatorCoeff(1), rhs.DenominatorCoeff(0)),
                                   WWComplex.Mul(lhs.DenominatorCoeff(0), rhs.DenominatorCoeff(1)));
            var d0 = WWComplex.Mul(lhs.DenominatorCoeff(0), rhs.DenominatorCoeff(0));

            return new SecondOrderRationalPolynomial(n2, n1, n0, d2, d1, d0);
        }

        /// <summary>
        /// p次オールポールの多項式(分子は多項式係数のリストで分母は根のリスト)を部分分数展開する。分子の多項式の次数はp次未満。
        ///
        ///             ... + nCoeffs[2]s^2 + nCoeffs[1]s + nCoeffs[0]
        /// F(s) = ----------------------------------------------------------
        ///          (s-dRoots[0])(s-dRoots[1])(s-dRoots[3])…(s-dRoots[p-1])
        ///          
        /// 戻り値は、多項式を部分分数分解した結果の、1次有理多項式のp個のリスト。
        /// 
        ///             c[0]            c[1]            c[2]             c[p-1]
        /// F(s) = ------------- + ------------- + ------------- + … + ---------------------
        ///         s-dRoots[0]     s-dRoots[1]     s-dRoots[2]         s-dRoots[p-1]
        /// </summary>
        public static List<FirstOrderRationalPolynomial> PartialFractionDecomposition(List<WWComplex> nCoeffs, List<WWComplex> dRoots) {
            var result = new List<FirstOrderRationalPolynomial>();

            if (dRoots.Count == 1 && nCoeffs.Count == 1) {
                result.Add(new FirstOrderRationalPolynomial(new WWComplex(0,0), nCoeffs[0], new WWComplex(1,0), WWComplex.Minus(dRoots[0])));
                return result;
            }

            if (dRoots.Count < 2) {
                throw new ArgumentException("dRoots");
            }
            if (dRoots.Count <= nCoeffs.Count) {
                throw new ArgumentException("nCoeffs");
            }


            for (int k = 0; k < dRoots.Count; ++k) {
                // cn = ... + nCoeffs[2]s^2 + nCoeffs[1]s + nCoeffs[0]
                // 係数c[0]は、s==dRoots[0]としたときの、cn/(s-dRoots[1])(s-dRoots[2])(s-dRoots[3])…(s-dRoots[p-1])
                // 係数c[1]は、s==dRoots[1]としたときの、cn/(s-dRoots[0])(s-dRoots[2])(s-dRoots[3])…(s-dRoots[p-1])
                // 係数c[2]は、s==dRoots[2]としたときの、cn/(s-dRoots[0])(s-dRoots[1])(s-dRoots[3])…(s-dRoots[p-1])

                // 分子の値c。
                var c = new WWComplex(0, 0);
                var s = new WWComplex(1,0);
                for (int j = 0; j < nCoeffs.Count; ++j) {
                    c.Add(WWComplex.Mul(nCoeffs[j], s));
                    s.Mul(dRoots[k]);
                }

                for (int i = 0; i < dRoots.Count; ++i) {
                    if (i == k) {
                        continue;
                    }

                    c.Div(WWComplex.Sub(dRoots[k], dRoots[i]));
                }

                result.Add(new FirstOrderRationalPolynomial(new WWComplex(0,0), c, new WWComplex(1,0), WWComplex.Minus(dRoots[k])));
            }

            return result;
        }

        public static void Test() {
            /*
             * 部分分数分解のテスト。
             *           s + 3
             * X(s) = -------------
             *         (s+1)s(s-2)
             * 
             * 部分分数分解すると
             * 
             *          2/3      -3/2      5/6
             * X(s) = ------- + ------ + -------
             *         s + 1       s      s - 2
             */
            var nPolynomialCoeffs = new List<WWComplex>();
            nPolynomialCoeffs.Add(new WWComplex(3, 0));
            nPolynomialCoeffs.Add(new WWComplex(1, 0));

            var dRoots = new List<WWComplex>();
            dRoots.Add(new WWComplex(-1, 0));
            dRoots.Add(new WWComplex(0, 0));
            dRoots.Add(new WWComplex(2, 0));

            var polynomialList = WWPolynomial.PartialFractionDecomposition(nPolynomialCoeffs, dRoots);

            for (int i = 0; i < polynomialList.Count; ++i) {
                Console.WriteLine(polynomialList[i].ToString("s"));
                if (i != polynomialList.Count - 1) {
                    Console.WriteLine(" + ");
                }
            }

            Console.WriteLine("");
        }
    }
}

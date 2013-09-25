using System;
using System.Text;

namespace Elekto.Mathematic
{
    /// <summary>
    /// Written by Fabrice Bellard on January 8, 1997.
    /// 
    /// We use a slightly modified version of the method described by Simon
    /// Plouffe in "On the Computation of the n'th decimal digit of various
    /// transcendental numbers" (November 1996). We have modified the algorithm
    /// to get a running time of O(n^2) instead of O(n^3log(n)^3).
    /// 
    /// This program uses mostly integer arithmetic. It may be slow on some
    /// hardwares where integer multiplications and divisons must be done
    /// by software. We have supposed that 'int' has a size of 32 bits. If
    /// your compiler supports 'long long' integers of 64 bits, you may use
    /// the integer version of 'MulMod' (see HAS_LONG_LONG).  
    /// 
    /// O propósito prático é criar um "benchmark" para o poder de CPU e memória.
    /// </summary>
    public static class PiCalculation
    {
        private static int MulMod(int a, int b, int m)
        {
            return (int) ((a*(long) b)%m);
        }


        /// <summary>
        /// return the inverse of x mod y
        /// </summary>
        private static int InvMod(int x, int y)
        {
            int u = x;
            int v = y;
            int c = 1;
            int a = 0;

            do
            {
                int q = v/u;

                int t = c;
                c = a - q*c;
                a = t;

                t = u;
                u = v - q*u;
                v = t;
            } while (u != 0);

            a = a%y;

            if (a < 0)
            {
                a = y + a;
            }

            return a;
        }


        /// <summary>
        /// return (a^b) mod m
        /// </summary>
        private static int PowMod(int a, int b, int m)
        {
            int r = 1;
            int aa = a;

            while (true)
            {
                if ((b & 1) != 0)
                {
                    r = MulMod(r, aa, m);
                }

                b = b >> 1;

                if (b == 0)
                {
                    break;
                }

                aa = MulMod(aa, aa, m);
            }

            return r;
        }

        /// <summary>
        /// return true if n is prime
        /// </summary>
        private static bool IsPrime(int n)
        {
            if ((n%2) == 0)
            {
                return false;
            }

            var r = (int) Math.Sqrt(n);

            for (int i = 3; i <= r; i += 2)
            {
                if ((n%i) == 0)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// return the prime number immediatly after n
        /// </summary>
        private static int NextPrime(int n)
        {
            do
            {
                n++;
            } while (!IsPrime(n));

            return n;
        }

        private static string CalculateNinePiDigits(int n)
        {
            var nn = (int) ((n + 20)*Math.Log(10)/Math.Log(2));

            double sum = 0;

            for (int a = 3; a <= (2*nn); a = NextPrime(a))
            {
                var vmax = (int) (Math.Log(2*nn)/Math.Log(a));

                int av = 1;

                for (int i = 0; i < vmax; i++)
                {
                    av = av*a;
                }

                int s = 0;
                int num = 1;
                int den = 1;
                int v = 0;
                int kq = 1;
                int kq2 = 1;

                int t;
                for (int k = 1; k <= nn; k++)
                {
                    t = k;

                    if (kq >= a)
                    {
                        do
                        {
                            t = t/a;
                            v--;
                        } while ((t%a) == 0);

                        kq = 0;
                    }
                    kq++;
                    num = MulMod(num, t, av);

                    t = 2*k - 1;

                    if (kq2 >= a)
                    {
                        if (kq2 == a)
                        {
                            do
                            {
                                t = t/a;
                                v++;
                            } while ((t%a) == 0);
                        }
                        kq2 -= a;
                    }
                    den = MulMod(den, t, av);
                    kq2 += 2;

                    if (v > 0)
                    {
                        t = InvMod(den, av);
                        t = MulMod(t, num, av);
                        t = MulMod(t, k, av);

                        for (int i = v; i < vmax; i++)
                        {
                            t = MulMod(t, a, av);
                        }

                        s += t;

                        if (s >= av)
                        {
                            s -= av;
                        }
                    }
                }

                t = PowMod(10, n - 1, av);
                s = MulMod(s, t, av);
                sum = (sum + s/(double) av)%1.0;
            }

            var result = (int) (sum*1e9);

            string stringResult = String.Format("{0:D9}", result);

            return stringResult;
        }

        /// <summary>
        /// Gets the pi.
        /// </summary>
        /// <param name="digits">The number of digits.</param>
        /// <returns></returns>
        public static string GetPi(int digits)
        {
            if (digits <= 0)
            {
                throw new ArgumentOutOfRangeException("digits", digits, "Shold be greater than zero.");
            }

            var result = new StringBuilder("3.", 1024);
            for (int i = 0; i < digits; i += 9)
            {
                String ds = CalculateNinePiDigits(i + 1);
                int digitCount = Math.Min(digits - i, 9);

                while (ds.Length < 9)
                {
                    ds = "0" + ds;
                }

                result.Append(ds.Substring(0, digitCount));
            }

            return result.ToString();
        }
    }
}
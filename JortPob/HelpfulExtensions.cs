using JortPob.Common;
using System.Numerics;

namespace JortPob
{
    public static class HelpfulExtensions
    {
        public static Vector3 AdjustByConst(this Vector3 vec)
        {
            return vec + Const.TEST_OFFSET1 + Const.TEST_OFFSET2;
        }
    }
}

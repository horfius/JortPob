using JortPob.Common;
using System.Numerics;

namespace JortPob
{
    public static class HelpfulExtensions
    {
        public static string ToCollisionIndex(this Int2 coords)
        {
            return $"{coords.x.ToString("D2")}{coords.y.ToString("D2")}";
        }

        public static Vector3 AdjustByConst(this Vector3 vec)
        {
            return vec + Const.TEST_OFFSET1 + Const.TEST_OFFSET2;
        }
    }
}

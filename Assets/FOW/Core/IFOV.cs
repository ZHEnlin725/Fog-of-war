using UnityEngine;

namespace FOW.Core
{
    /// <summary>
    /// 视野接口 Field Of View
    /// </summary>
    public interface IFOV
    {
        /// <summary>
        /// 更新视野的可见性
        /// </summary>
        void UpdateVisible();

        /// <summary>
        /// 该视野内是否可见
        /// </summary>
        /// <returns></returns>
        bool Visible();

        /// <summary>
        /// 获取该视野的半径
        /// </summary>
        /// <returns></returns>
        float getRadius();

        /// <summary>
        /// 获取该视野的位置
        /// </summary>
        /// <returns></returns>
        Vector3 getPosition();
    }
}
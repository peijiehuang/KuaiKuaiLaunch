using System;
using System.Collections.Generic;

namespace KuaiKuaiLaunch.Models
{
    /// <summary>
    /// 快捷分类数据实体（对应左侧导航栏分类）
    /// </summary>
    public class Category
    {
        /// <summary>
        /// 分类唯一标识 GUID
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// 分类显示名称
        /// </summary>
        public string Name { get; set; } = "新分类";

        /// <summary>
        /// WPF-UI SymbolRegular 图标枚举字符串，例如 "Apps24", "Briefcase24", "Globe24"
        /// </summary>
        public string IconSymbol { get; set; } = "Apps24";

        /// <summary>
        /// 分类排序索引
        /// </summary>
        public int OrderIndex { get; set; } = 0;

        /// <summary>
        /// 分类下属分组列表
        /// </summary>
        public List<ShortcutGroup> Groups { get; set; } = new();
    }
}


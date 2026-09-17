using System;
using System.Collections.Generic;

namespace KuaiKuaiLaunch.Models
{
    /// <summary>
    /// 快捷启动分组数据实体
    /// </summary>
    public class ShortcutGroup
    {
        /// <summary>
        /// 分组唯一标识 GUID
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// 分组显示名称
        /// </summary>
        public string Name { get; set; } = "新建分组";

        /// <summary>
        /// 是否展开显示
        /// </summary>
        public bool IsExpanded { get; set; } = true;

        /// <summary>
        /// 排序索引
        /// </summary>
        public int OrderIndex { get; set; } = 0;

        /// <summary>
        /// 分组下包含的快捷项目列表
        /// </summary>
        public List<ShortcutItem> Items { get; set; } = new();
    }
}


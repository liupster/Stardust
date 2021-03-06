﻿using System;
using System.ComponentModel;
using System.Xml.Serialization;
using Stardust.Models;
using XCode;
using XCode.Membership;

namespace Stardust.Data
{
    /// <summary>应用在线。一个应用有多个部署，每个在线会话对应一个服务地址</summary>
    public partial class AppOnline : EntityBase<AppOnline>
    {
        #region 对象操作
        static AppOnline()
        {
            // 累加字段
            //var df = Meta.Factory.AdditionalFields;
            //df.Add(__.AppID);

            // 过滤器 UserModule、TimeModule、IPModule
            Meta.Modules.Add<TimeModule>();
            Meta.Modules.Add<IPModule>();
        }

        /// <summary>验证数据，通过抛出异常的方式提示验证失败。</summary>
        /// <param name="isNew">是否插入</param>
        public override void Valid(Boolean isNew)
        {
            // 如果没有脏数据，则不需要进行任何处理
            if (!HasDirty) return;

            // 在新插入数据或者修改了指定字段时进行修正
            //if (isNew && !Dirtys[nameof(CreateTime)]) nameof(CreateTime) = DateTime.Now;
            //if (!Dirtys[nameof(UpdateTime)]) nameof(UpdateTime) = DateTime.Now;
            //if (isNew && !Dirtys[nameof(CreateIP)]) nameof(CreateIP) = ManageProvider.UserHost;

            // 检查唯一索引
            // CheckExist(isNew, __.Session);
            // CheckExist(isNew, __.AppID, __.Instance);
        }
        #endregion

        #region 扩展属性
        /// <summary>应用</summary>
        [XmlIgnore]
        //[ScriptIgnore]
        public App App => Extends.Get(nameof(App), k => App.FindByID(AppID));

        /// <summary>应用</summary>
        [XmlIgnore]
        //[ScriptIgnore]
        [DisplayName("应用")]
        [Map(__.AppID, typeof(App), "ID")]
        public String AppName => App?.Name;
        #endregion

        #region 扩展查询
        /// <summary>根据编号查找</summary>
        /// <param name="id">编号</param>
        /// <returns>实体对象</returns>
        public static AppOnline FindByID(Int32 id)
        {
            if (id <= 0) return null;

            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.ID == id);

            // 单对象缓存
            return Meta.SingleCache[id];

            //return Find(_.ID == id);
        }

        /// <summary>根据会话查找</summary>
        /// <param name="session">会话</param>
        /// <returns>实体对象</returns>
        public static AppOnline FindBySession(String session)
        {
            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Session == session);

            return Find(_.Session == session);
        }
        #endregion

        #region 高级查询
        #endregion

        #region 业务操作
        /// <summary>获取 或 创建 会话</summary>
        /// <param name="session"></param>
        /// <returns></returns>
        public static AppOnline GetOrAddSession(String session)
        {
            return GetOrAdd(session, (k, c) => Find(_.Session == k), k => new AppOnline { Session = k });
        }

        /// <summary>更新信息</summary>
        /// <param name="app"></param>
        /// <param name="info"></param>
        public void UpdateInfo(App app, AppInfo info)
        {
            AppID = app.ID;
            Name = app.Name;
            UserName = info.UserName;
            ProcessId = info.Id;
            ProcessName = info.Name;
            StartTime = info.StartTime;

            SaveAsync();
        }
        #endregion
    }
}
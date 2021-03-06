﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Caching;
using NewLife.Data;
using XCode;
using XCode.Membership;

namespace Stardust.Data.Monitors
{
    /// <summary>跟踪分钟统计。每应用每接口每5分钟统计，用于分析接口健康状况</summary>
    public partial class TraceMinuteStat : Entity<TraceMinuteStat>
    {
        #region 对象操作
        private static ICache _cache = Cache.Default;
        static TraceMinuteStat()
        {
            // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
            var df = Meta.Factory.AdditionalFields;
            df.Add(nameof(Total));
            df.Add(nameof(Errors));
            df.Add(nameof(TotalCost));

            // 过滤器 UserModule、TimeModule、IPModule
            Meta.Modules.Add<TimeModule>();
        }

        /// <summary>验证并修补数据，通过抛出异常的方式提示验证失败。</summary>
        /// <param name="isNew">是否插入</param>
        public override void Valid(Boolean isNew)
        {
            // 如果没有脏数据，则不需要进行任何处理
            if (!HasDirty) return;

            // 建议先调用基类方法，基类方法会做一些统一处理
            base.Valid(isNew);

            Cost = Total == 0 ? 0 : (Int32)(TotalCost / Total);
        }
        #endregion

        #region 扩展属性
        /// <summary>应用</summary>
        [XmlIgnore, IgnoreDataMember]
        public AppTracer App => Extends.Get(nameof(App), k => AppTracer.FindByID(AppId));

        /// <summary>应用</summary>
        [Map(nameof(AppId))]
        public String AppName => App + "";
        #endregion

        #region 扩展查询
        /// <summary>根据编号查找</summary>
        /// <param name="id">编号</param>
        /// <returns>实体对象</returns>
        public static TraceMinuteStat FindByID(Int32 id)
        {
            if (id <= 0) return null;

            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.ID == id);

            // 单对象缓存
            return Meta.SingleCache[id];

            //return Find(_.ID == id);
        }

        /// <summary>根据应用和时间查找</summary>
        /// <param name="appId"></param>
        /// <param name="time"></param>
        /// <returns></returns>
        public static IList<TraceMinuteStat> FindAllByAppIdAndTime(Int32 appId, DateTime time) => FindAll(_.AppId == appId & _.StatTime == time);

        /// <summary>查询某应用某天的所有统计，带缓存</summary>
        /// <param name="appId"></param>
        /// <param name="date"></param>
        /// <returns></returns>
        public static IList<TraceMinuteStat> FindAllByAppIdWithCache(Int32 appId, DateTime date)
        {
            var key = $"TraceMinuteStat:FindAllByAppIdWithCache:{appId}#{date:yyyyMMdd}";
            if (_cache.TryGetValue<IList<TraceMinuteStat>>(key, out var list) && list != null) return list;

            // 查询数据库，即时空值也缓存，避免缓存穿透
            list = FindAll(_.AppId == appId & _.StatTime >= date & _.StatTime < date.AddDays(1));

            _cache.Set(key, list, 10);

            return list;
        }
        #endregion

        #region 高级查询
        /// <summary>高级查询</summary>
        /// <param name="appId">应用</param>
        /// <param name="name">操作名。接口名或埋点名</param>
        /// <param name="start">统计日期开始</param>
        /// <param name="end">统计日期结束</param>
        /// <param name="key">关键字</param>
        /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
        /// <returns>实体列表</returns>
        public static IList<TraceMinuteStat> Search(Int32 appId, String name, DateTime start, DateTime end, String key, PageParameter page)
        {
            var exp = new WhereExpression();

            if (appId >= 0) exp &= _.AppId == appId;
            if (!name.IsNullOrEmpty()) exp &= _.Name == name;
            exp &= _.StatTime.Between(start, end);
            if (!key.IsNullOrEmpty()) exp &= _.Name.Contains(key);

            return FindAll(exp, page);
        }

        /// <summary>查找一批统计</summary>
        /// <param name="time"></param>
        /// <param name="appIds"></param>
        /// <returns></returns>
        public static IList<TraceMinuteStat> Search(DateTime time, Int32[] appIds) => FindAll(_.StatTime == time & _.AppId.In(appIds));

        /// <summary>指定应用根据名称分组统计</summary>
        /// <param name="appId">应用</param>
        /// <param name="start">统计日期开始</param>
        /// <param name="end">统计日期结束</param>
        /// <returns></returns>
        public static IList<TraceMinuteStat> SearchGroup(Int32 appId, DateTime start, DateTime end)
        {
            var selects = _.Total.Sum() & _.Errors.Sum() & _.TotalCost.Sum() & _.MaxCost.Max() & _.MinCost.Min() & _.Name;

            var exp = new WhereExpression();
            exp &= _.AppId == appId;
            exp &= _.StatTime >= start & _.StatTime < end;

            return FindAll(exp.GroupBy(_.Name), null, selects);
        }
        #endregion

        #region 业务操作
        private static TraceMinuteStat FindByTrace(TraceStatModel model, Boolean cache)
        {
            var key = $"TraceMinuteStat:FindByTrace:{model.Key}";
            if (cache && _cache.TryGetValue<TraceMinuteStat>(key, out var st)) return st;

            st = FindAllByAppIdWithCache(model.AppId, model.Time.Date)
                .FirstOrDefault(e => e.StatTime == model.Time && e.Name.EqualIgnoreCase(model.Name));

            // 查询数据库
            if (st == null) st = Find(_.StatTime == model.Time & _.AppId == model.AppId & _.Name == model.Name);

            if (st != null) _cache.Set(key, st, 60);

            return st;
        }

        /// <summary>查找统计行</summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public static TraceMinuteStat FindOrAdd(TraceStatModel model)
        {
            // 高并发下获取或新增对象
            return GetOrAdd(model, FindByTrace, m => new TraceMinuteStat { StatTime = m.Time, AppId = m.AppId, Name = m.Name });
        }

        /// <summary>删除指定时间之前的数据</summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public static Int32 DeleteBefore(DateTime time) => Delete(_.StatTime < time);
        #endregion
    }
}
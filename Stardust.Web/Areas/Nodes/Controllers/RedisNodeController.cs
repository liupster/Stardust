﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Data;
using NewLife.Log;
using NewLife.Web;
using Stardust.Data.Nodes;
using Stardust.Server.Services;
using XCode;
using XCode.Membership;

namespace Stardust.Web.Areas.Nodes.Controllers
{
    [NodesArea]
    public class RedisNodeController : EntityController<RedisNode>
    {
        private readonly IRedisService _redisService;

        static RedisNodeController() => MenuOrder = 50;

        public RedisNodeController(IRedisService redisService) => _redisService = redisService;

        protected override IEnumerable<RedisNode> Search(Pager p)
        {
            var nodeId = p["Id"].ToInt(-1);
            if (nodeId > 0)
            {
                var node = RedisNode.FindById(nodeId);
                if (node != null) return new[] { node };
            }

            var category = p["category"];
            var server = p["server"];
            var enable = p["enable"]?.ToBoolean();

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            return RedisNode.Search(server, category, enable, start, end, p["Q"], p);
        }

        /// <summary>搜索</summary>
        /// <param name="provinceId"></param>
        /// <param name="cityId"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public ActionResult NodeSearch(String category, String key = null)
        {
            var page = new PageParameter { PageSize = 20 };

            // 默认排序
            if (page.Sort.IsNullOrEmpty()) page.Sort = RedisNode._.Name;

            var list = RedisNode.Search(null, category, true, DateTime.MinValue, DateTime.MinValue, key, page);

            return Json(0, null, list.Select(e => new
            {
                e.Id,
                e.Name,
                e.Server,
                e.Category,
            }).ToArray());
        }

        [EntityAuthorize(PermissionFlags.Update)]
        public ActionResult Refresh(Int32 id)
        {
            var node = RedisNode.FindById(id);
            if (node != null)
            {
                XTrace.WriteLine("刷新 {0}/{1} {2}", node.Name, node.Id, node.Server);

                try
                {
                    _redisService.TraceNode(node);

                    var queues = RedisMessageQueue.FindAllByRedisId(node.Id);
                    foreach (var item in queues)
                    {
                        _redisService.TraceQueue(item);
                        item.SaveAsync();
                    }
                }
                catch (Exception ex)
                {
                    LogProvider.Provider.WriteLog("RedisNode", "Refresh", false, ex?.GetMessage());

                    throw;
                }
            }

            return JsonRefresh($"刷新[{node}]成功！");
        }

        protected override Boolean Valid(RedisNode entity, DataObjectMethodType type, Boolean post)
        {
            if (!post) return base.Valid(entity, type, post);

            var act = type switch
            {
                DataObjectMethodType.Update => "修改",
                DataObjectMethodType.Insert => "添加",
                DataObjectMethodType.Delete => "删除",
                _ => type + "",
            };

            // 必须提前写修改日志，否则修改后脏数据失效，保存的日志为空
            if (type == DataObjectMethodType.Update && (entity as IEntity).HasDirty)
                LogProvider.Provider.WriteLog(act, entity);

            var err = "";
            try
            {
                return base.Valid(entity, type, post);
            }
            catch (Exception ex)
            {
                err = ex.Message;
                throw;
            }
            finally
            {
                LogProvider.Provider.WriteLog(act, entity, err);
            }
        }
    }
}
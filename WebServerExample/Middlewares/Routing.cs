﻿using System;
using System.Collections.Generic;
using System.Reflection;
using WebServerExample.Infrastructure;
using WebServerExample.Infrastructure.Results;
using WebServerExample.Interfaces;
using WebServerExample.Models;

namespace WebServerExample.Middlewares
{
    /// <summary>
    /// Url Routing
    /// </summary>
    public class Routing : IMiddleware
    {
        public Routing()
        {
            _entries = new List<RouteEntry>();
        }

        private List<RouteEntry> _entries;

        /// <summary>
        /// Map route
        /// </summary>
        /// <param name="name"></param>
        /// <param name="url"></param>
        /// <param name="defaults"></param>
        /// <returns></returns>
        public Routing MapRoute(string name, string url, object defaults = null)
        {
            _entries.Add(new RouteEntry(name, url, defaults));
            return this;
        }

        public MiddlewareResult Execute(HttpServerContext context)
        {
            foreach (var entry in _entries)
            {
                var routeValues = entry.Match(context.Request);
                if (routeValues != null)
                {
                    var controller = CreateController(context, routeValues);
                    var actionMethod = GetActionMethod(controller, routeValues);
                    var result = GetActionResult(controller, actionMethod, routeValues);
                    result.Execute(context);
                    
                    return MiddlewareResult.Processed;
                }
            }

            return MiddlewareResult.Continue;
        }

        private IController CreateController(HttpServerContext context, RouteValueDictionary routeValues)
        {
            var controllerName = (string)routeValues["controller"];
            var className = char.ToUpper(controllerName[0]) + controllerName.Substring(1) + "Controller";
            foreach (var type in GetType().Assembly.GetExportedTypes())
            {
                if (type.Name == className && typeof(Controller).IsAssignableFrom(type))
                {
                    var instance = (Controller) Activator.CreateInstance(type);
                    instance.HttpContext = context;
                    return instance;
                }
            }
            throw new ArgumentException($"Controller {className} not found");
        }

        private MethodInfo GetActionMethod(IController controller, RouteValueDictionary routeValues)
        {
            var controllerType = controller.GetType();
            string actionName = (string) routeValues["action"];
            actionName = char.ToUpper(actionName[0]) + actionName.Substring(1);
            var method = controller.GetType().GetMethod(actionName);
            if (method == null)
                throw new ArgumentException($"Controller {controllerType.Name} has no action method {actionName}");
            return method;
        }

        private ActionResult GetActionResult(IController controller, MethodInfo method,
            RouteValueDictionary routeValues)
        {
            var methodParams = method.GetParameters();
            var paramValues = new object[methodParams.Length];
            for (int i = 0; i < methodParams.Length; i++)
            {
                var routeValue = routeValues[methodParams[i].Name];
                var paramValue = Convert.ChangeType(routeValue, methodParams[i].ParameterType);
                paramValues[i] = paramValue;
            }
            
            var result = method.Invoke(controller, paramValues);
            var actionResult = result as ActionResult;
            if (actionResult != null)
                return actionResult;
            else
                return new ContentResult(Convert.ToString(result), "text/html");
        }
    }
}
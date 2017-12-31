﻿#if NETCOREAPP2_0

namespace Rollbar.AspNetCore
{
    using Microsoft.AspNetCore.Http;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Implements an Asp.Net Core middleware component
    /// that invokes the Rollbar.NET component as a catcher all
    /// possible uncaught exceptions while executing/invoking
    /// other/inner middleware components.
    /// </summary>
    /// <example>
    /// Usage example:
    /// To register this middleware component, implement Startup.Configure(...) of your 
    /// Asp.Net Core application as
    /// 
    /// <code>
    /// // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    /// public void Configure(IApplicationBuilder app, IHostingEnvironment env)
    /// {
    ///     if (env.IsDevelopment())
    ///     {
    ///         app.UseDeveloperExceptionPage();
    ///     }
    /// 
    ///     app.UseRollbarMiddleware();
    ///     
    ///     // All the middleware components intended to be "monitored"
    ///     // by the Rollbar middleware to be added below this line:
    ///     app.UseAnyOtherInnerMiddleware();
    /// 
    ///     app.UseMvc();
    /// }
    /// 
    /// </code>
    /// 
    /// </example>
    public class RollbarMiddleware
    {
        /// <summary>
        /// The next request processor within the middleware pipeline
        /// </summary>
        protected readonly RequestDelegate _nextRequestProcessor = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="RollbarMiddleware"/> class.
        /// </summary>
        /// <param name="nextRequestProcessor">The next request processor.</param>
        public RollbarMiddleware(RequestDelegate nextRequestProcessor)
        {
            _nextRequestProcessor = nextRequestProcessor;
        }

        /// <summary>
        /// Invokes this middleware instance on the specified context.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>A middleware invocation/execution task.</returns>
        public virtual async Task Invoke(HttpContext context)
        {
            try
            {
                await this._nextRequestProcessor(context);
            }
            catch(Exception ex)
            {
                RollbarLocator.RollbarInstance.Critical(ex);

                throw new Exception("The included internal exception processed by the Rollbar middleware", ex);
            }
        }
    }
}

#endif

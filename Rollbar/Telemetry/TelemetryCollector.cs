﻿[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("UnitTest.Rollbar")]

namespace Rollbar.Telemetry
{
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Threading;
    using Rollbar.DTOs;

    /// <summary>
    /// Implements Rollbar telemetry collector service.
    /// </summary>
    public class TelemetryCollector
        : ITelemetryCollector
    {
        #region singleton implementation

        /// <summary>
        /// Gets the instance.
        /// </summary>
        /// <value>
        /// The instance.
        /// </value>
        public static TelemetryCollector Instance
        {
            get
            {
                return NestedSingleInstance.Instance;
            }
        }

        /// <summary>
        /// Prevents a default instance of the <see cref="TelemetryCollector"/> class from being created.
        /// </summary>
        private TelemetryCollector()
        {
            this._config = new TelemetryConfig();
            this._config.Reconfigured += _config_Reconfigured;

            this.StartAutocollection();
        }

        private sealed class NestedSingleInstance
        {
            static NestedSingleInstance()
            {
            }

            /// <summary>
            /// The singleton-like instance of the service.
            /// </summary>
            internal static readonly TelemetryCollector Instance =
                new TelemetryCollector();
        }

        #endregion singleton implementation

        /// <summary>
        /// Captures the specified telemetry.
        /// </summary>
        /// <param name="telemetry">The telemetry.</param>
        /// <returns></returns>
        public TelemetryCollector Capture(Telemetry telemetry)
        {
            if (this.Config.TelemetryEnabled)
            {
                this._telemetryQueue.Enqueue(telemetry);
            }
            return this;
        }

        /// <summary>
        /// Captures the specified telemetry.
        /// </summary>
        /// <param name="telemetry">The telemetry.</param>
        /// <returns></returns>
        ITelemetryCollector ITelemetryCollector.Capture(Telemetry telemetry)
        {
            return this.Capture(telemetry);
        }

        /// <summary>
        /// Gets the items count.
        /// </summary>
        /// <returns></returns>
        public int GetItemsCount()
        {
            return this._telemetryQueue.GetItemsCount();
        }

        /// <summary>
        /// Gets the content of the queue.
        /// </summary>
        /// <returns></returns>
        public Telemetry[] GetQueueContent()
        {
            return this._telemetryQueue.GetQueueContent();
        }

        /// <summary>
        /// Flushes the queue.
        /// </summary>
        /// <returns></returns>
        public ITelemetryCollector FlushQueue()
        {
            this._telemetryQueue.Flush();
            return this;
        }

        /// <summary>
        /// Gets the configuration.
        /// </summary>
        /// <value>
        /// The configuration.
        /// </value>
        public TelemetryConfig Config { get { return this._config; } }

        /// <summary>
        /// Starts the auto-collection.
        /// </summary>
        public ITelemetryCollector StartAutocollection()
        {
            if (!this.Config.TelemetryEnabled)
            {
                return this; // no need to start at all...
            }
            if (this.Config.TelemetryAutoCollectionTypes == TelemetryType.None)
            {
                return this; // nothing really to collect...
            }
            if (this.Config.TelemetryAutoCollectionInterval == TimeSpan.Zero)
            {
                return this; // this, essentially, means the auto-collection is off...
            }

            // let's resync with relevant config settings: 
            this._telemetryQueue.QueueDepth = this.Config.TelemetryQueueDepth;

            lock (_syncRoot)
            {
                if (this._telemetryThread == null)
                {
                    this._telemetryThread = new Thread(new ParameterizedThreadStart(this.KeepCollectingTelemetry))
                    {
                        IsBackground = true,
                        Name = "Rollbar Telemetry Thread"
                    };

                    this._cancellationTokenSource = new CancellationTokenSource();
                    this._telemetryThread.Start(_cancellationTokenSource.Token);
                }
            }

            return this;
        }

        /// <summary>
        /// Stops the auto-collection.
        /// </summary>
        /// <param name="immediate">if set to <c>true</c> [immediate].</param>
        public ITelemetryCollector StopAutocollection(bool immediate)
        {
            lock(_syncRoot)
            {
                if (this._cancellationTokenSource == null)
                {
                    return this;
                }

                this._cancellationTokenSource.Cancel();
                if (immediate && this._telemetryThread != null)
                {
                    this._telemetryThread.Join(TimeSpan.FromSeconds(60));
                    CompleteProcessing();
                }
                return this;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is auto-collecting.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is auto-collecting; otherwise, <c>false</c>.
        /// </value>
        public bool IsAutocollecting
        {
            get
            {
                lock(this._syncRoot)
                {
                    return !(this._cancellationTokenSource == null || this._cancellationTokenSource.IsCancellationRequested);
                }
            }
        }

        /// <summary>
        /// Gets the queue depth.
        /// </summary>
        /// <value>
        /// The queue depth.
        /// </value>
        public int QueueDepth { get { return this._telemetryQueue.QueueDepth; } }

        private readonly TelemetryConfig _config;
        private readonly TelemetryQueue _telemetryQueue = new TelemetryQueue();
        private Thread _telemetryThread = null;
        private CancellationTokenSource _cancellationTokenSource = null;
        private readonly object _syncRoot = new object();

        private void _config_Reconfigured(object sender, EventArgs e)
        {
            this._telemetryQueue.QueueDepth = this._config.TelemetryQueueDepth;

            if (this.IsAutocollecting && this._config.TelemetryAutoCollectionInterval == TimeSpan.Zero)
            {
                this.StopAutocollection(false);
            }
        }

        private void CompleteProcessing()
        {
            if (this._cancellationTokenSource == null)
            {
                return;
            }

            Debug.WriteLine("Entering " + this.GetType().FullName + "." + nameof(this.CompleteProcessing) + "() method...");
            this._cancellationTokenSource.Dispose();
            this._cancellationTokenSource = null;
            this._telemetryThread = null;
        }

        private void AutoCollectTelemetry()
        {
            //TBD...
        }

        private void KeepCollectingTelemetry(object data)
        {
            //for now, until AutoCollectTelemetry() is implemented:
            return;

            CancellationToken cancellationToken = (CancellationToken)data;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    this.AutoCollectTelemetry();
                }
#pragma warning disable CS0168 // Variable is declared but never used
                catch (System.Threading.ThreadAbortException tae)
                {
                    return;
                }
                catch (System.Exception ex)
                {
                    string msg = ex.Message;
                    //TODO: do we want to direct the exception 
                    //      to some kind of Rollbar notifier maintenance "access token"?
                }
#pragma warning restore CS0168 // Variable is declared but never used

                if (cancellationToken.IsCancellationRequested)
                    break;

                Thread.Sleep(this.Config.TelemetryAutoCollectionInterval);
            }

            CompleteProcessing();
        }
    }
}

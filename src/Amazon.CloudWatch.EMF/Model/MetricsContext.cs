using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Amazon.CloudWatch.EMF.Utils;

namespace Amazon.CloudWatch.EMF.Model
{
    public class MetricsContext
    {
        private readonly RootNode _rootNode;

        /// <summary>
        /// holds a reference to _rootNode.MetaData.CloudWatchDirective;
        /// </summary>
        private readonly MetricDirective _metricDirective;

        /// <summary>
        /// Holds the metric key and its resolution type for validation checks
        /// </summary>
        private readonly ConcurrentDictionary<string, StorageResolution> _metricNameAndResolutionMap = new ConcurrentDictionary<string, StorageResolution>();

        public MetricsContext() : this(new RootNode())
        {
        }

        public MetricsContext(RootNode rootNode)
        {
            if (rootNode == null) throw new ArgumentNullException(nameof(rootNode));
            _rootNode = rootNode;
            _metricDirective = rootNode.AWS.MetricDirective;
        }

        public MetricsContext(
            string logNamespace,
            Dictionary<string, object> properties,
            List<DimensionSet> dimensionSets,
            DimensionSet defaultDimensionSet) : this()
        {
            if (string.IsNullOrEmpty(logNamespace)) throw new ArgumentNullException(nameof(logNamespace));
            if (properties == null) throw new ArgumentNullException(nameof(properties));
            if (dimensionSets == null) throw new ArgumentNullException(nameof(dimensionSets));
            if (defaultDimensionSet == null) throw new ArgumentNullException(nameof(defaultDimensionSet));

            Namespace = logNamespace;
            DefaultDimensions = defaultDimensionSet;
            foreach (DimensionSet dimension in dimensionSets)
            {
                PutDimension(dimension);
            }

            foreach (var property in properties)
            {
                PutProperty(property.Key, property.Value);
            }
        }

        /// <summary>
        /// Creates a new MetricsContext with the same namespace, properties,
        /// and dimensions as this one but empty metrics-directive collection.
        /// Custom dimensions are preserved by default unless preservedDimension is set to false
        /// </summary>
        /// <returns></returns>
        public MetricsContext CreateCopyWithContext(bool preserveDimensions = true)
        {
            return new MetricsContext(
                    _metricDirective.Namespace,
                    _rootNode.GetProperties(),
                    preserveDimensions ? _metricDirective.CustomDimensionSets : new List<DimensionSet>(),
                    _metricDirective.DefaultDimensionSet);
        }

        /// <summary>
        /// Gets or sets the namespace for all metrics in this context.
        /// </summary>
        public string Namespace
        {
            get { return _metricDirective.Namespace; }
            set { _metricDirective.Namespace = value; }
        }

        /// <summary>
        /// Gets or Sets the default dimensions for the context.
        /// If no custom dimensions are specified, the metrics will be emitted with these defaults.
        /// If custom dimensions are specified, they will be prepended with these default dimensions
        /// </summary>
        public DimensionSet DefaultDimensions
        {
            get { return _metricDirective.DefaultDimensionSet; }
            set { _metricDirective.DefaultDimensionSet = value; }
        }

        /// <summary>
        /// Indicates whether default dimensions have already been set on this context.
        /// </summary>
        public bool HasDefaultDimensions
        {
            get { return DefaultDimensions.DimensionKeys.Count > 0; }
        }

        /// <summary>
        /// Add a metric measurement to the context.
        /// Multiple calls using the same key will be stored as an array of scalar values.
        /// </summary>
        /// <example>
        /// Defaults to Standard Resolution : metricContext.PutMetric("Latency", 100)
        /// Standard Resolution metric : metricContext.PutMetric("Latency", 100, Unit.MILLISECONDS)
        /// High Resolution metric : metricContext.PutMetric("Latency", 100, Unit.MILLISECONDS,StorageResolution.HIGH)
        /// </example>
        /// <param name="key">the name of the metric</param>
        /// <param name="value">the value of the metric</param>
        /// <param name="unit">the units of the metric</param>
        /// <param name="storageResolution">the storage resolution of the metric. Defaults to StandardResolution</param>
        public void PutMetric(string key, double value, Unit unit, StorageResolution storageResolution = StorageResolution.STANDARD)
        {
            Validator.ValidateMetric(key, value, storageResolution, _metricNameAndResolutionMap);
            _metricDirective.PutMetric(key, value, unit, storageResolution);
            _metricNameAndResolutionMap.TryAdd(key, storageResolution);
        }

        /// <summary>
        /// Add a metric measurement to the context without a unit.
        /// Multiple calls using the same key will be stored as an array of scalar values.
        /// </summary>
        /// <example>
        /// Defaults to Standard Resolution : metricContext.PutMetric("Count", 10)
        /// StandardResolution metric : metricContext.PutMetric("Count", 10, Unit.MILLISECONDS)
        /// HighResolution metric : metricContext.PutMetric("Count", 100, Unit.MILLISECONDS,StorageResolution.HIGH)
        /// </example>
        /// <param name="key">the name of the metric</param>
        /// <param name="value">the value of the metric</param>
        /// <param name="storageResolution">the storage resolution of the metric. Defaults to StandardResolution</param>
        public void PutMetric(string key, double value, StorageResolution storageResolution = StorageResolution.STANDARD)
        {
            PutMetric(key, value, Unit.NONE, storageResolution);
        }

        /// <summary>
        /// Add a property to this log entry.
        /// Properties are additional values that can be associated with metrics.
        /// They will not show up in CloudWatch metrics, but they are searchable in CloudWatch Insights.
        /// </summary>
        /// <example>
        /// metricContext.PutProperty("Location", 'US')
        /// </example>
        /// <param name="name">the name of the property</param>
        /// <param name="value">the value of the property</param>
        public void PutProperty(string name, object value)
        {
            _rootNode.PutProperty(name, value);
        }

        /// <summary>
        /// Gets the value of the property with the specified name.
        /// </summary>
        /// <param name="name"></param>
        /// <returns>the value of the property with the specified name, or <c>null</c> if no property with that name has been set</returns>
        public object GetProperty(string name)
        {
            _rootNode.GetProperties().TryGetValue(name, out var value);
            return value;
        }

        /// <summary>
        /// Add dimensions to the metric context.
        /// </summary>
        /// <param name="dimensionSet">the dimensions set to add.</param>
        public void PutDimension(DimensionSet dimensionSet)
        {
            _metricDirective.PutDimension(dimensionSet);
        }

        /// <summary>
        /// Adds a dimension set with single dimension-value entry to the metric context.
        /// </summary>
        /// <example>
        /// metricContext.PutDimension("ExampleDimension", "ExampleValue")
        /// </example>
        /// <param name="dimension">the name of the new dimension.</param>
        /// <param name="value">the value of the new dimension.</param>
        public void PutDimension(string dimension, string value)
        {
            var dimensionSet = new DimensionSet();
            dimensionSet.AddDimension(dimension, value);
            _metricDirective.PutDimension(dimensionSet);
        }

        /// <summary>
        /// Gets all dimension sets that have been added, including default dimensions.
        /// </summary>
        /// <returns>the list of dimensions that has been added, including default dimensions.</returns>
        public List<DimensionSet> GetAllDimensionSets()
        {
            return _metricDirective.GetAllDimensionSets();
        }

        /// <summary>
        /// Update the dimensions to the specified list; also overriding default dimensions
        /// </summary>
        /// <param name="dimensionSets">the dimensionSets to use instead of all existing dimensions and default dimensions.</param>
        public void SetDimensions(params DimensionSet[] dimensionSets)
        {
            _metricDirective.SetDimensions(dimensionSets.ToList());
        }

        /// <summary>
        /// Update the dimensions to the specified list; optionally overriding default dimensions
        /// </summary>
        /// <param name="useDefault">whether to use default dimensions or not.</param>
        /// <param name="dimensionSets">the dimensionSets to use instead of all existing dimensions and default dimensions.</param>
        public void SetDimensions(bool useDefault, params DimensionSet[] dimensionSets)
        {
            _metricDirective.SetDimensions(useDefault, dimensionSets.ToList());
        }

        /// <summary>
        /// Reset all dimensions
        /// </summary>
        /// <param name="useDefault">whether to keep default dimensions or not.</param>
        public void ResetDimensions(bool useDefault)
        {
            _metricDirective.ResetDimensions(useDefault);
        }

        /// <summary>
        /// Adds the specified key-value pair to the metadata.
        /// </summary>
        /// <param name="key">the name of the key.</param>
        /// <param name="value">the value to associate with the specified key.</param>
        public void PutMetadata(string key, object value)
        {
            _rootNode.AWS.CustomMetadata.Add(key, value);
        }

        /// <summary>
        /// Serializes the metrics in this context to strings.
        /// The EMF backend requires no more than 100 metrics in one log event.
        /// If there are more than 100 metrics, we split the metrics into multiple log events (strings).
        /// </summary>
        /// <returns>the serialized strings</returns>
        public List<string> Serialize()
        {
            var nodes = new List<RootNode>();
            if (_rootNode.AWS.MetricDirective.Metrics.Count <= Constants.MaxMetricsPerEvent)
            {
                nodes.Add(_rootNode);
            }
            else
            {
                // split the root nodes into multiple and serialize each
                var count = 0;
                while (count < _rootNode.AWS.MetricDirective.Metrics.Count)
                {
                    var metrics = _rootNode.AWS.MetricDirective.Metrics.Skip(count).Take(Constants.MaxMetricsPerEvent).ToList();
                    var node = _rootNode.DeepCloneWithNewMetrics(metrics);
                    nodes.Add(node);
                    count += Constants.MaxMetricsPerEvent;
                }
            }

            var results = new List<string>();
            foreach (var node in nodes)
            {
                results.Add(node.Serialize());
            }

            return results;
        }
    }
}
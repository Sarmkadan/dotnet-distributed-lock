// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Formatters;

using System.Xml;
using System.Xml.Serialization;
using SarmKadan.DistributedLock.Core.Models;

/// <summary>
/// XML serializer for lock data structures.
/// Provides XML formatting for enterprise integration scenarios.
/// Handles proper XML encoding and namespace management.
/// </summary>
public class XmlLockSerializer
{
    private static readonly XmlSerializerNamespaces _namespaces = new();
    private static readonly XmlWriterSettings _writerSettings = new()
    {
        Indent = true,
        IndentChars = "  ",
        Encoding = System.Text.Encoding.UTF8,
        OmitXmlDeclaration = false
    };

    static XmlLockSerializer()
    {
        _namespaces.Add("", "http://sarmkadan.com/distributedlock/2026");
    }

    /// <summary>
    /// Serializes a lock object to XML string.
    /// </summary>
    public static string SerializeLock(Lock @lock)
    {
        if (@lock == null)
            return string.Empty;

        try
        {
            var serializer = new XmlSerializer(typeof(Lock));
            using (var writer = new StringWriter())
            {
                using (var xmlWriter = XmlWriter.Create(writer, _writerSettings))
                {
                    serializer.Serialize(xmlWriter, @lock, _namespaces);
                }

                return writer.ToString();
            }
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException("Failed to serialize lock to XML", ex);
        }
    }

    /// <summary>
    /// Serializes multiple locks to XML format.
    /// Wraps locks in a root element with metadata.
    /// </summary>
    public static string SerializeLocks(IEnumerable<Lock> locks)
    {
        try
        {
            var lockList = locks.ToList();
            var xDoc = new XmlDocument();
            var rootElement = xDoc.CreateElement("Locks");
            rootElement.SetAttribute("xmlns", "http://sarmkadan.com/distributedlock/2026");
            rootElement.SetAttribute("Count", lockList.Count.ToString());
            rootElement.SetAttribute("ExportTime", DateTime.UtcNow.ToString("O"));

            foreach (var @lock in lockList)
            {
                var lockXml = SerializeLock(@lock);
                var lockDoc = new XmlDocument();
                lockDoc.LoadXml(lockXml);

                if (lockDoc.DocumentElement != null)
                {
                    var importedNode = xDoc.ImportNode(lockDoc.DocumentElement, true);
                    rootElement.AppendChild(importedNode);
                }
            }

            xDoc.AppendChild(rootElement);

            using (var writer = new StringWriter())
            {
                using (var xmlWriter = XmlWriter.Create(writer, _writerSettings))
                {
                    xDoc.WriteTo(xmlWriter);
                }

                return writer.ToString();
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to serialize locks to XML", ex);
        }
    }

    /// <summary>
    /// Deserializes an XML string to a lock object.
    /// </summary>
    public static Lock? DeserializeLock(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return null;

        try
        {
            var serializer = new XmlSerializer(typeof(Lock));
            using (var reader = new StringReader(xml))
            {
                return (Lock?)serializer.Deserialize(reader);
            }
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException("Failed to deserialize lock from XML", ex);
        }
    }

    /// <summary>
    /// Deserializes an XML document containing multiple locks.
    /// </summary>
    public static List<Lock> DeserializeLocks(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return new List<Lock>();

        try
        {
            var xDoc = new XmlDocument();
            xDoc.LoadXml(xml);

            var locks = new List<Lock>();
            var lockElements = xDoc.GetElementsByTagName("Lock");

            foreach (XmlElement lockElement in lockElements)
            {
                var lockXml = lockElement.OuterXml;
                var @lock = DeserializeLock(lockXml);

                if (@lock != null)
                    locks.Add(@lock);
            }

            return locks;
        }
        catch (XmlException ex)
        {
            throw new InvalidOperationException("Failed to deserialize locks from XML", ex);
        }
    }

    /// <summary>
    /// Exports lock metrics in XML format.
    /// </summary>
    public static string ExportMetrics(IEnumerable<LockMetrics> metrics)
    {
        try
        {
            var xDoc = new XmlDocument();
            var rootElement = xDoc.CreateElement("LockMetrics");
            rootElement.SetAttribute("xmlns", "http://sarmkadan.com/distributedlock/2026");
            rootElement.SetAttribute("ExportTime", DateTime.UtcNow.ToString("O"));

            foreach (var metric in metrics)
            {
                var metricElement = xDoc.CreateElement("Metric");
                metricElement.SetAttribute("LockId", metric.Id);

                AddXmlElement(xDoc, metricElement, "AcquisitionAttempts", metric.AcquisitionAttempts.ToString());
                AddXmlElement(xDoc, metricElement, "SuccessfulAcquisitions", metric.SuccessfulAcquisitions.ToString());
                AddXmlElement(xDoc, metricElement, "FailedAcquisitions", metric.FailedAcquisitions.ToString());
                AddXmlElement(xDoc, metricElement, "AverageHoldTimeMs", metric.AverageHoldTimeMs.ToString());
                AddXmlElement(xDoc, metricElement, "MaxHoldTimeMs", metric.MaxHoldTimeMs.ToString());
                AddXmlElement(xDoc, metricElement, "ContentionCount", metric.ContentionCount.ToString());
                AddXmlElement(xDoc, metricElement, "LastAcquisitionTime", metric.LastAcquisitionTime.ToString("O"));

                rootElement.AppendChild(metricElement);
            }

            xDoc.AppendChild(rootElement);

            using (var writer = new StringWriter())
            {
                using (var xmlWriter = XmlWriter.Create(writer, _writerSettings))
                {
                    xDoc.WriteTo(xmlWriter);
                }

                return writer.ToString();
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to export metrics to XML", ex);
        }
    }

    private static void AddXmlElement(XmlDocument doc, XmlElement parent, string elementName, string? value)
    {
        var element = doc.CreateElement(elementName);
        element.InnerText = value ?? string.Empty;
        parent.AppendChild(element);
    }
}

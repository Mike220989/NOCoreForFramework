﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Xml.Linq;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Runtime;

namespace NetworkObservabilityCore
{
    public class GraphXML
    {
		private XDocument file;

		public XDocument File
		{
			get => file;
			set { file = value; }
		}

		public Dictionary<String, Assembly> DependencyMap
		{
			get;
			set;
		}

		public GraphXML()
			: this("1.0", "utf-8", "false")
		{
		}
		
		public GraphXML(String version, String encoding, String standalone)
		{
			File = new XDocument(new XDeclaration(version, encoding, standalone));
			DependencyMap = new Dictionary<String, Assembly>();
		}

		public void Save(String path, IGraph graph)
		{
			XElement root = new XElement("NetworkObservabilityCore");
			DumpTo(graph, ref root);
			File.Add(root);

			// New way to save
			File.Save(path);

			// Old way to save
			/*
			var xml = System.IO.File.Create(path);
			var writer = new StreamWriter(xml);
			File.Save(writer);
			writer.Dispose();
			*/
		}

		public void DumpTo(IGraph graph, ref XElement root)
		{
			XElement dependenciesNode = new XElement("Dependencies");

			Type graphType = graph.GetType();
			XElement graphNode = new XElement("Graph", new XAttribute("Type", graphType.FullName));
			DependencyMap[graphType.FullName] = graphType.GetTypeInfo().Assembly;

			XElement nodes = new XElement("Nodes");
			foreach (var pair in graph.AllNodes)
			{
				INode node = pair.Value;
				nodes.Add(CreateXElement(node));
			}
			graphNode.Add(nodes);

			XElement edges = new XElement("Edges");
			foreach (var pair in graph.AllEdges)
			{
				IEdge edge = pair.Value;
				edges.Add(CreateXElement(edge));
			}
			graphNode.Add(edges);

			var dependencies = DependencyMap.Values.Distinct();
			foreach (var dependency in dependencies)
			{
				var types = DependencyMap.Where(type => type.Value.FullName == dependency.FullName);
				XElement dependencyNode = new XElement("Dependency", new XAttribute("Name", dependency.ManifestModule.Name));
				foreach(var typePair in types)
				{
					dependencyNode.Add(new XElement("Type", typePair.Key));
				}
				dependenciesNode.Add(dependencyNode);
			}

			XElement indexGeneratorNode = new XElement("IndexGenerator");
			indexGeneratorNode.Add(new XElement("NodeIdIndex", IdGenerator.nodeIdIndex.ToString()));
			indexGeneratorNode.Add(new XElement("EdgeIdIndex", IdGenerator.edgeIdIndex.ToString()));

			root.Add(dependenciesNode);
			root.Add(indexGeneratorNode);
			root.Add(graphNode);
		}

		public XElement CreateXElement(INode node)
		{
			Type nodeType = node.GetType();
			XElement xelement = new XElement("Node", new XAttribute("Id", node.Id), new XAttribute("Type", nodeType.FullName));
			DependencyMap[nodeType.FullName] = nodeType.GetTypeInfo().Assembly;

			var isObserver = new XElement("IsObserver", node.IsObserver);
			var isObserverInclusive = new XElement("IsObserverInclusive", node.IsObserverInclusive);
			var isVisible = new XElement("IsVisible", node.IsVisible);
			var isBlocked = new XElement("IsBlocked", node.IsBlocked);
			var label = new XElement("Label", node.Label);
			var attributes = CreateAttributes(node.Attributes);

			xelement.Add(isObserver);
			xelement.Add(isObserverInclusive);
			xelement.Add(isVisible);
			xelement.Add(isBlocked);
			xelement.Add(label);
			xelement.Add(attributes);

			return xelement;
		}

		public XElement CreateXElement(IEdge edge)
		{
			Type edgeType = edge.GetType();
			XElement xelement = new XElement("Edge", new XAttribute("Id", edge.Id), new XAttribute("Type", edgeType.FullName));
			DependencyMap[edgeType.FullName] = edgeType.GetTypeInfo().Assembly;

			var from = new XElement("From", edge.From.Id);
			var label = new XElement("Label", edge.Label);
			var to = new XElement("To", edge.To.Id);
			var isBlocked = new XElement("IsBlocked", edge.IsBlocked);
			var weight = new XElement("Weight", edge.Weight);
			var attributes = CreateAttributes(edge.Attributes);

			xelement.Add(from);
			xelement.Add(label);
			xelement.Add(to);
			xelement.Add(isBlocked);
			xelement.Add(weight);
			xelement.Add(attributes);

			return xelement;
		}

		public XElement CreateAttributes<K, V>(IDictionary<K, V> attributes)
		{
			XElement xelement = new XElement("Attributes");
			foreach (var pair in attributes)
			{
				var xattribute = new XElement("Attribute");
				var xkey = new XAttribute("Key", pair.Key);
				var xvalue = new XAttribute("Value", pair.Value);
				Type valueType = pair.Value.GetType();
				var typeFullName = valueType.FullName;
				var xvalueType = new XAttribute("ValueType", typeFullName);
				if (!typeFullName.Contains("System"))
					DependencyMap[typeFullName] = valueType.GetTypeInfo().Assembly;
				xattribute.Add(xkey, xvalue, xvalueType);

				xelement.Add(xattribute);
			}
			return xelement;
		}

		public IGraph Read(String path)
		{
			File = XDocument.Load(path);
			IGraph graph = Dump(File.Root);

			return graph;
		}

		public IGraph Dump(XElement root)
		{
			XElement dependencies = root.Element("Dependencies");
			foreach (var dependency in dependencies.Elements())
			{
				String assemPath = dependency.Attribute("Name").Value;

				foreach (var type in dependency.Elements("Type"))
				{
					DependencyMap[type.Value] = Assembly.LoadFrom(assemPath);
				}
			}

			XElement xgraph = root.Element("Graph");

			var graphTypeName = xgraph.Attribute("Type").Value;
			Type graphType = DependencyMap[graphTypeName].GetType(graphTypeName);

			IGraph graph = Activator.CreateInstance(graphType) as IGraph;

			IEnumerable<XElement> xnodes = xgraph.Element("Nodes").Elements();

			foreach (XElement xnode in xnodes)
			{
				LoadNodeToGraph(xnode, graph);
			}

			IEnumerable<XElement> xedges = xgraph.Element("Edges").Elements();

			foreach (XElement xedge in xedges)
			{
				LoadEdgeToGraph(xedge, graph);
			}

			SetIdsStartFrom(root);

			return graph;
		}

		public void SetIdsStartFrom(XElement root)
		{
			var xnode = root.Element("IndexGenerator");
			IdGenerator.SetNodeIdStartFrom(Int32.Parse(xnode.Element("NodeIdIndex").Value));
			IdGenerator.SetEdgeIdStartFrom(Int32.Parse(xnode.Element("EdgeIdIndex").Value));

		}

		public void LoadNodeToGraph(XElement xnode, IGraph graph)
		{
			var nodeTypeName = xnode.Attribute("Type").Value;
			Type nodeType = DependencyMap[nodeTypeName].GetType(nodeTypeName);
			INode node = Activator.CreateInstance(nodeType) as INode;

			PropertyInfo property = nodeType.GetProperty("Id", BindingFlags.NonPublic | 
				BindingFlags.Public | BindingFlags.Instance);
			property.SetValue(node, xnode.Attribute("Id").Value);
			node.IsObserver = Boolean.Parse(xnode.Element("IsObserver").Value);
			node.IsObserverInclusive = Boolean.Parse(xnode.Element("IsObserverInclusive").Value);
			node.IsVisible = Boolean.Parse(xnode.Element("IsVisible").Value);
			node.Label = xnode.Element("Label").Value;
			node.IsBlocked = Boolean.Parse(xnode.Element("IsBlocked").Value);
			node.Attributes = LoadAttributes(xnode.Element("Attributes"));
			node.ConnectTo = new List<IEdge>();
			node.ConnectFrom = new List<IEdge>();

			graph.Add(node);
		}

		public void LoadEdgeToGraph(XElement xedge, IGraph graph)
		{
			var edgeTypeName = xedge.Attribute("Type").Value;
			Type edgeType = DependencyMap[edgeTypeName].GetType(edgeTypeName);
			IEdge edge = Activator.CreateInstance(edgeType) as IEdge;

			PropertyInfo property = edgeType.GetProperty("Id", BindingFlags.NonPublic | 
				BindingFlags.Public | BindingFlags.Instance);
			property.SetValue(edge, xedge.Attribute("Id").Value);
			edge.Label = xedge.Element("Label").Value;
			edge.IsBlocked = Boolean.Parse(xedge.Element("IsBlocked").Value);
			edge.Attributes = LoadAttributes(xedge.Element("Attributes"));

			var from = graph.AllNodes[xedge.Element("From").Value];
			var to = graph.AllNodes[xedge.Element("To").Value];

			graph.ConnectNodeToWith(from, to, edge);
		}

		public IDictionary<String, IComparable> LoadAttributes(XElement xelement)
		{
			var attributes = new Dictionary<String, IComparable>();
			
			foreach (var xattribute in xelement.Elements())
			{
				var xkey = xattribute.Attribute("Key").Value;
				var xvalue = xattribute.Attribute("Value").Value;
				var valueTypeName = xattribute.Attribute("ValueType").Value;
				Type valueType;
				IComparable attribute;
				if (valueTypeName.Contains("System"))
				{
					valueType = Type.GetType(valueTypeName);
				}
				else
				{
					valueType = DependencyMap[valueTypeName].GetType(valueTypeName);
				}
				var value = ChangeType(xvalue, valueType);
				if (valueType.Equals(typeof(String)))
				{
					attribute = value;
				}
				else if (HasConstructor(valueType))
				{
					attribute = Activator.CreateInstance(valueType, value) as IComparable;
				}
				else
				{
					attribute = Activator.CreateInstance(valueType) as IComparable;
					attribute = value;
				}
				attributes[xkey] = attribute;
			}
			return attributes;
		}

		private bool HasConstructor(Type type)
		{
			var hasConstructor = type.GetConstructor(BindingFlags.Default, null, Type.EmptyTypes, null) != null;
			return !(type.IsPrimitive || hasConstructor);
		}

		private IComparable ChangeType(String str, Type type)
		{
			return Convert.ChangeType(str, type) as IComparable;
		}

	}
}

﻿using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkObservabilityCore
{
    public class Node : INode
    {

		#region Property
		public String Id
		{
			get;
			protected set;
		}

		public String Label
		{
			get;
			set;
		}

		public List<IEdge> Neighbours
		{
			get;
			set;
		}

		public List<IEdge> ConnectFrom
		{
			get;
			set;
		}

		public bool IsObserver
		{
			get;
			set;
		}

		public bool IsObserverInclusive
		{
			get;
			set;
		}

		public bool IsVisible
		{
			get;
			set;
		}

		#endregion

		#region Constructors
		public Node()
		{
			Id = IdGenerator.GenerateNodeIndex();
			Label = Id;
			Neighbours = new List<IEdge>();
			ConnectFrom = new List<IEdge>();
			IsObserver = IsObserverInclusive = false;
			IsVisible = true;
		}
		#endregion

		public override String ToString()
		{
			return String.Format("{0}: {1}", Id, Label);
		}

		public override int GetHashCode()
		{
			return Id.GetHashCode();
		}

		bool IEquatable<INode>.Equals(INode other)
		{
			return Id == other.Id;
		}
	}
}

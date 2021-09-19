﻿using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Reseed.Ordering;

namespace Reseed.Graphs
{
	internal sealed class OrderedGraph<T> where T : class
	{
		public static readonly OrderedGraph<T> Empty = new OrderedGraph<T>(
			Array.Empty<OrderedItem<T>>(),
			Array.Empty<MutualReference<T>>());

		public readonly IReadOnlyCollection<OrderedItem<T>> Nodes;
		public readonly IReadOnlyCollection<MutualReference<T>> MutualReferences;

		public OrderedGraph(
			[NotNull] IReadOnlyCollection<OrderedItem<T>> nodes,
			[NotNull] IReadOnlyCollection<MutualReference<T>> mutualReferences)
		{
			if (nodes == null) throw new ArgumentNullException(nameof(nodes));
			if (nodes.Count == 0) throw new ArgumentException("Value cannot be an empty collection.", nameof(nodes));

			this.Nodes = nodes;
			this.MutualReferences = mutualReferences ?? throw new ArgumentNullException(nameof(mutualReferences));
		}

		public OrderedGraph<TOut> MapShallow<TOut>([NotNull] Func<T, TOut> mapper) where TOut : class
		{
			if (mapper == null) throw new ArgumentNullException(nameof(mapper));
			return new OrderedGraph<TOut>(
				this.Nodes
					.Select(o => o.Map(mapper))
					.ToArray(),
				this.MutualReferences.Select(r => r.Map(mapper)).ToArray());
		}

		public OrderedGraph<T> FilterShallow([NotNull] Func<T, bool> predicate) 
		{
			if (predicate == null) throw new ArgumentNullException(nameof(predicate));
			return new OrderedGraph<T>(
				this.Nodes.Where(o => predicate(o.Value)).ToArray(),
				this.MutualReferences
					.Where(r => r.Items.All(predicate))
					.ToArray());
		}

		public OrderedGraph<T> Reverse()
		{
			int maxIndex = this.Nodes.Count - 1;

			return new OrderedGraph<T>(
				this.Nodes.Select(n => n.MapOrder(i => maxIndex - i)).ToArray(),
				this.MutualReferences);
		}
	}

	internal static class OrderedGraphExtensions
	{
		public static OrderedGraph<T> FilterDeep<T>(
			[NotNull] this OrderedGraph<T> graph,
			[NotNull] Func<T, bool> predicate) 
			where T : class, IMutableNode<T>
		{
			if (graph == null) throw new ArgumentNullException(nameof(graph));
			if (predicate == null) throw new ArgumentNullException(nameof(predicate));
			
			var backMapping = new Dictionary<T, T>();
			Dictionary<T, T> mapping = graph.Nodes
				.Select(o => o.Value)
				.Where(predicate)
				.MapDeep<T, T>(
					(r, n) => r.Map(_ => n),
					(n, rs) =>
					{
						T nn = n.With(rs
							.Where(r => predicate(r.Target))
							.ToArray());

						backMapping.Add(nn, n);
						return nn;
					})
				.ToDictionary(n => backMapping[n]);

			return new OrderedGraph<T>(
				graph.Nodes
					.Where(o => mapping.ContainsKey(o.Value))
					.Select(o => o.Map(n => mapping[n]))
					.ToArray(),
				graph.MutualReferences
					.Where(r => r.Items.All(mapping.ContainsKey))
					.Select(r => r.Map(n => mapping[n]))
					.ToArray());
		}
	}
}
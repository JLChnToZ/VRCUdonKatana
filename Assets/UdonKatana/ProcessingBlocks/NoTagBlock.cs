using System;
using System.Collections.Generic;
using JLChnToZ.Katana.Expressions;

namespace JLChnToZ.VRC.UdonKatana {
    [ProcessingBlockPriority(Priority = int.MaxValue)]
    internal class NoTagBlock: ProcessingBlock {
        static readonly HashSet<Node> noArgsNode = new HashSet<Node>();

        public static bool IsNoArgsNode(Node node) => noArgsNode.Contains(node);

        public NoTagBlock(Node current, AssemblerState state)
            : base(current, state) {}

        protected override bool BeforeResolveBlockType() {
            if (current.Tag == null && current.Count == 1 && current[0].Count == 0)
                noArgsNode.Add(current[0]);
            return true;
        }

        protected override bool ResolveBlockType(out Type type) {
            if (current.Tag == null && current.Count > 0) {
                state.tagTypeMapping.TryGetValue(current[current.Count - 1], out type);
                foreach (var child in current) noArgsNode.Remove(child); // Cleanup
                return true;
            }
            type = null;
            return false;
        }

        protected override bool Process(Stack<ProcessingBlock> stack) {
            if (i >= current.Count) return true;
            stack.Push(Create(current[i], state, i == current.Count - 1 ? ExplicitTarget : default));
            i++;
            return false;
        }
    }
}


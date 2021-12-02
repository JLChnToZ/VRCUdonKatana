using System;
using System.Collections.Generic;
using System.Reflection;
using VRC.Udon;
using JLChnToZ.VRC.UdonLowLevel;
using JLChnToZ.Katana.Expressions;

namespace JLChnToZ.VRC.UdonKatana {
    public abstract class ProcessingBlock {
        protected static readonly VariableName returnValue = UdonBehaviour.ReturnVariableName;
        private static readonly List<Type> blockTypes = new List<Type>();
        private readonly List<VariableName> rentVariables = new List<VariableName>();
        public readonly Node current;
        protected readonly AssemblerState state;
        protected VariableName ExplicitTarget { get; private set; }
        protected int i;
        protected VariableName result;

        static ProcessingBlock() {
            var currentDomain = AppDomain.CurrentDomain;
            currentDomain.AssemblyLoad += OnAssemblyLoad;
            foreach(var assembly in currentDomain.GetAssemblies())
                RegisterTypes(assembly);
        }

        public static void AssembleBody(Node root, UdonAssemblyBuilder builder, string source = null) {
            var stack = new Stack<ProcessingBlock>();
            var state = new AssemblerState(builder);
            ResolveTypes(root, state, source);
            stack.Push(Create(root, state));
            while (stack.Count > 0) {
                ProcessingBlock block = stack.Peek();
                try {
                    if (block.Process(stack))
                        stack.Pop();
                } catch (Exception ex) {
                    throw new CompileException(ex.Message, ex, block.current, source);
                }
            }
        }

        protected static ProcessingBlock Create(Node node, AssemblerState state, VariableName explicitTarget = default) {
            var processingBlock = state.processingBlocks[node];
            processingBlock.ExplicitTarget = explicitTarget;
            return processingBlock;
        }

        private static void ResolveTypes(Node node, AssemblerState state, string source) {
            var stack = new Stack<Node>();
            var queue = new Queue<Node>();
            queue.Enqueue(node);
            // Walk through the node tree from the deepest.
            var ctorArguments = new object[] { node, state };
            var cache = new Dictionary<(Node, Type), ProcessingBlock>();
            var parentMap = new Dictionary<Node, Node>();
            while (queue.Count > 0) {
                var current = queue.Dequeue();
                stack.Push(current);
                ctorArguments[0] = current;
                bool shouldWalk = true;
                foreach (var type in blockTypes) {
                    var processingBlock = Activator.CreateInstance(type, ctorArguments) as ProcessingBlock;
                    cache[(current, type)] = processingBlock;
                    if (!processingBlock.BeforeResolveBlockType()) {
                        shouldWalk = false;
                        break;
                    }
                }
                if (shouldWalk)
                    foreach (var child in current) {
                        parentMap[child] = current;
                        queue.Enqueue(child);
                    }
            }
            while (stack.Count > 0) {
                var current = stack.Pop();
                bool matches = false;
                foreach (var type in blockTypes)
                    if (cache.TryGetValue((current, type), out var pb) && pb.ResolveBlockType(out var targetType)) {
                        if (targetType != null) state.tagTypeMapping[current] = targetType;
                        state.processingBlocks[current] = pb;
                        matches = true;
                        break;
                    }
                if (!matches) throw new CompileException($"No matching handler for node `{current.Tag}`", current, source);
            }
        }

        private static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args) => RegisterTypes(args.LoadedAssembly);

        private static void RegisterTypes(Assembly assembly) {
            foreach (var type in assembly.GetTypes())
                if (type.IsClass && !type.IsAbstract && type.IsSubclassOf(typeof(ProcessingBlock)))
                    blockTypes.Add(type);
            blockTypes.Sort(CompareBlockTypes);
        }

        private static int CompareBlockTypes(Type lhs, Type rhs) => Comparer<int>.Default.Compare(
            ProcessingBlockPriorityAttribute.GetAttribute(rhs)?.Priority ?? 0,
            ProcessingBlockPriorityAttribute.GetAttribute(lhs)?.Priority ?? 0
        );

        protected ProcessingBlock(
            Node current,
            AssemblerState state
        ) {
            this.current = current;
            this.state = state;
        }

        protected virtual bool BeforeResolveBlockType() => true;

        protected abstract bool ResolveBlockType(out Type type);

        protected abstract bool Process(Stack<ProcessingBlock> stack);

        protected void ReturnAllTempVariables() {
            rentVariables.ForEach(state.ReturnVariable);
            rentVariables.Clear();
        }

        protected VariableName GetTempVariable(Node forNode) {
            var result = state.RentVariable(forNode);
            rentVariables.Add(result);
            return result;
        }

        protected VariableName GetTempVariable(Type type) {
            var result = state.RentVariable(type);
            rentVariables.Add(result);
            return result;
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class ProcessingBlockPriorityAttribute: Attribute {
        public int Priority { get; set; }

        public static ProcessingBlockPriorityAttribute GetAttribute(Type type) =>
            GetCustomAttribute(type, typeof(ProcessingBlockPriorityAttribute)) as ProcessingBlockPriorityAttribute;
    }

    public class CompileException: Exception {
        public SourcePosition Position { get; private set; }

        public override string Message => $"Compile Error: {base.Message} at line {Position.line}, column {Position.column}";

        public CompileException(Node node, string source) {
            Position = node?.GetPosition(source) ?? default;
        }

        public CompileException(string message, Node node, string source) : base(message) {
            Position = node?.GetPosition(source) ?? default;
        }

        public CompileException(string message, Exception innerException, Node node, string source) : base(message, innerException) {
            Position = node?.GetPosition(source) ?? default;
        }
    }
}
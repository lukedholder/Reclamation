using System.Collections.Generic;

namespace Reclamation.Simulation
{
    /// <summary>
    /// Stores all live constructs keyed by ID.
    /// ConstructSystem is the only writer; other systems and the view layer read.
    /// </summary>
    public class ConstructTable
    {
        private readonly Dictionary<int, Construct> _constructs = new();
        private int _nextId = 1;

        /// <summary>Number of constructs currently alive.</summary>
        public int Count => _constructs.Count;

        /// <summary>Returns the construct with the given ID, or null if not found.</summary>
        public Construct Get(int id) =>
            _constructs.TryGetValue(id, out var c) ? c : null;

        /// <summary>
        /// Allocates a new construct with a unique ID and adds it to the table.
        /// The caller (ConstructSystem) is responsible for populating BlockIds and
        /// calling Reclassify before the construct is visible to other systems.
        /// </summary>
        public Construct Create()
        {
            var construct = new Construct { Id = _nextId++ };
            _constructs[construct.Id] = construct;
            return construct;
        }

        /// <summary>Removes the construct. Called when a construct becomes empty after block removal.</summary>
        public bool Remove(int id) => _constructs.Remove(id);

        /// <summary>Iterates all live constructs. Do not modify the table during enumeration.</summary>
        public IEnumerable<Construct> All => _constructs.Values;
    }
}

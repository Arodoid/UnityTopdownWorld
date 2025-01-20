namespace EntitySystem.Data
{
    public readonly struct EntityHandle
    {
        public readonly int Id;
        public readonly uint Version;

        public static readonly EntityHandle Invalid = new EntityHandle(-1, 0);

        public EntityHandle(int id, uint version)
        {
            Id = id;
            Version = version;
        }

        public bool IsValid => Id >= 0;

        public override bool Equals(object obj)
        {
            if (obj is EntityHandle other)
            {
                return Id == other.Id && Version == other.Version;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode() ^ Version.GetHashCode();
        }

        public static bool operator ==(EntityHandle a, EntityHandle b)
        {
            return a.Id == b.Id && a.Version == b.Version;
        }

        public static bool operator !=(EntityHandle a, EntityHandle b)
        {
            return !(a == b);
        }
    }
} 
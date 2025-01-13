namespace EntitySystem.Core.Jobs
{
    public static class JobPriorities
    {
        // Personal/Emergency Jobs (90-100)
        public const float FLEE_DANGER = 100f;
        public const float EXTINGUISH_SELF = 99f;
        
        // Personal/Survival Jobs (80-89)
        public const float FIND_FOOD = 85f;
        public const float FIND_SLEEP = 84f;
        public const float SEEK_SHELTER = 83f;
        
        // World/Emergency Jobs (70-79)
        public const float EXTINGUISH_FIRE = 75f;
        public const float RESCUE_ALLY = 74f;
        
        // World/Important Jobs (60-69)
        public const float HARVEST_CROPS = 65f;
        public const float REPAIR_CRITICAL = 64f;
        
        // World/Normal Jobs (50-59)
        public const float CHOP_WOOD = 55f;
        public const float MINE_RESOURCES = 54f;
        
        // World/Low Priority Jobs (40-49)
        public const float HAUL_ITEMS = 45f;
        public const float CLEAN_FLOOR = 44f;
        
        // Idle/Filler Jobs (1-39)
        public const float WANDER = 25f;
        public const float SOCIALIZE = 24f;
    }
} 
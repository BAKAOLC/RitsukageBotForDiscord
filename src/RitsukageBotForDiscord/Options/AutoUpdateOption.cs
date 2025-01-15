namespace RitsukageBot.Options
{
    internal class AutoUpdateOption
    {
        public bool Enable { get; init; } = false;

        public long CheckInterval { get; init; } = 3600000;

        public ProgramInformationOption Information { get; init; } = new();

        internal class ProgramInformationOption
        {
            public string RepositoryOwner { get; init; } = string.Empty;

            public string RepositoryName { get; init; } = string.Empty;

            public string BranchName { get; init; } = string.Empty;

            public string TargetJobName { get; init; } = string.Empty;
        }
    }
}
namespace Aruun.Core.Play
{
    /// <summary>Where a co-op client's actions go: to the host, over the wire.</summary>
    public interface ICoopLink
    {
        void Send(string kind, string body);
    }

    public sealed partial class Host
    {
        /// <summary>
        /// Set on a co-op client. The host there only mirrors the real one: Tick runs nothing, and
        /// each action is sent to the real host instead of being done here. Null when this host
        /// runs the world (single player, or the player hosting).
        /// </summary>
        public ICoopLink Link;

        /// <summary>On a client: how worn the block being dug is, as the real host last said.</summary>
        public double RemoteDig;

        public bool IsMirror { get { return Link != null; } }

        /// <summary>Applies an edit the real host made (client side), and queues it for drawing.</summary>
        public void ApplyRemote(Edits.EditOp op) { Apply(op); }
    }
}

namespace Redline.Scripts.Editor.DiscordRPC {
    public static class RpcStateInfo {
        public static string StateName(this RpcState state) {
            return state
                switch {
                    RpcState.Editmode => "Modifying",
                    RpcState.Playmode => "Testing",
                    RpcState.Uploadpanel => "Updating content",
                    _ => "Idle"
                };
        }
    }

    public enum RpcState {
        Editmode = 0,
        Playmode = 1,
        Uploadpanel = 2
    }
}
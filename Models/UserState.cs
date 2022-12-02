namespace botTelegram.Models
{
    public enum UserState
    {
        Menu,
        WaitingNick,
        WaitingJoinCode,
        WaitingAboutMe,
        NoUsing_WaitingCreateEventCode,
        WaitingInfoCreateEvent, 
        WainingLeaveCode, 
        NoUsing_WaitingDeleteEventCode, 
        WaitingRemovedEventCode, 
        WaitingPhoto,
        WaitingAdminRassword
    }
}
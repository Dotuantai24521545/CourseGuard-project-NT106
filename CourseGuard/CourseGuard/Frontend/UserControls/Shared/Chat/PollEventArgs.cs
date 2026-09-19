using System;

namespace CourseGuard.Frontend.UserControls.Shared.Chat
{
    public sealed class PollVoteEventArgs : EventArgs
    {
        public PollVoteEventArgs(int messageId, int pollId, int optionId)
        {
            MessageId = messageId;
            PollId = pollId;
            OptionId = optionId;
        }

        public int MessageId { get; }
        public int PollId { get; }
        public int OptionId { get; }
    }

    public sealed class PollCloseEventArgs : EventArgs
    {
        public PollCloseEventArgs(int messageId, int pollId)
        {
            MessageId = messageId;
            PollId = pollId;
        }

        public int MessageId { get; }
        public int PollId { get; }
    }
}

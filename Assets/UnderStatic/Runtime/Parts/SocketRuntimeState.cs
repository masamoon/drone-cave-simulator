using System;

namespace UnderStatic.Parts
{
    [Serializable]
    public sealed class SocketRuntimeState
    {
        public string socketId;
        public string occupiedPartInstanceId;
        public float insertionProgress;
        public float lockRotationProgress;
        public bool latchClosed;
        public bool latchOpenedForExtraction;
        public bool procedureOpenedForExtraction;
        public float[] fastenerProgress;
    }
}

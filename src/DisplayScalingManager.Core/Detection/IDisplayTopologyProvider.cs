namespace DisplayScalingManager.Core.Detection;

public interface IDisplayTopologyProvider
{
    DisplayTopology GetCurrentTopology();
    
}

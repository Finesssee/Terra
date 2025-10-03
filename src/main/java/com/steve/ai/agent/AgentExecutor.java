package com.steve.ai.agent;

import com.steve.ai.entity.SteveEntity;
import java.util.List;
import java.util.ArrayList;
import java.util.Map;

public class AgentExecutor {
    private final SteveEntity steve;
    private final List<ToolWrapper> tools;
    private final AgentChain chain;
    private int maxIterations = 10;
    
    public AgentExecutor(SteveEntity steve) {
        this.steve = steve;
        this.tools = initializeTools();
        this.chain = new AgentChain(steve);
    }
    
    private List<ToolWrapper> initializeTools() {
        List<ToolWrapper> toolList = new ArrayList<>();
        toolList.add(new ToolWrapper("build", "Build structures"));
        toolList.add(new ToolWrapper("mine", "Mine resources"));
        toolList.add(new ToolWrapper("attack", "Combat actions"));
        toolList.add(new ToolWrapper("pathfind", "Navigate to locations"));
        return toolList;
    }
    
    public AgentResponse execute(String input) {
        Map<String, Object> inputs = Map.of(
            "input", input,
            "tools", tools,
            "steve_context", getAgentContext()
        );
        
        AgentChain.ChainResult result = chain.invoke(inputs);
        
        return new AgentResponse(
            result.success,
            result.output,
            result.state
        );
    }
    
    private Map<String, Object> getAgentContext() {
        return Map.of(
            "name", steve.getSteveName(),
            "health", steve.getHealth(),
            "position", steve.blockPosition(),
            "available_tools", tools.size()
        );
    }
    
    public static class AgentResponse {
        public final boolean success;
        public final String output;
        public final Map<String, Object> intermediateSteps;
        
        public AgentResponse(boolean success, String output, Map<String, Object> steps) {
            this.success = success;
            this.output = output;
            this.intermediateSteps = steps;
        }
    }
}

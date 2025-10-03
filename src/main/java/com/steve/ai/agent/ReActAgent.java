package com.steve.ai.agent;

import com.steve.ai.entity.SteveEntity;
import java.util.*;

public class ReActAgent {
    private final SteveEntity steve;
    private final List<String> thoughtHistory;
    private final ConversationalMemory memory;
    private final AgentExecutor executor;
    
    public ReActAgent(SteveEntity steve) {
        this.steve = steve;
        this.thoughtHistory = new ArrayList<>();
        this.memory = new ConversationalMemory(4096);
        this.executor = new AgentExecutor(steve);
    }
    
    public AgentResult reason(String observation) {
        String thought = generateThought(observation);
        thoughtHistory.add(thought);
        
        String action = selectAction(thought);
        String actionInput = extractActionInput(thought);
        
        AgentExecutor.AgentResponse response = executor.execute(actionInput);
        
        String finalAnswer = synthesizeAnswer(response);
        
        return new AgentResult(thought, action, actionInput, finalAnswer);
    }
    
    private String generateThought(String observation) {
        List<VectorStore.EmbeddingEntry> relevantMemories = 
            memory.searchRelevantMemories(observation, 3);
        
        return "Thought: I need to analyze " + observation + 
               " considering my past experiences";
    }
    
    private String selectAction(String thought) {
        if (thought.contains("build")) return "build";
        if (thought.contains("mine")) return "mine";
        if (thought.contains("attack")) return "attack";
        return "think";
    }
    
    private String extractActionInput(String thought) {
        return thought.substring(thought.indexOf(":") + 1).trim();
    }
    
    private String synthesizeAnswer(AgentExecutor.AgentResponse response) {
        memory.addAssistantMessage(response.output);
        return "Action: " + response.output;
    }
    
    public List<String> getThoughtHistory() {
        return new ArrayList<>(thoughtHistory);
    }
    
    public static class AgentResult {
        public final String thought;
        public final String action;
        public final String actionInput;
        public final String observation;
        
        public AgentResult(String thought, String action, String actionInput, String observation) {
            this.thought = thought;
            this.action = action;
            this.actionInput = actionInput;
            this.observation = observation;
        }
    }
}

package com.steve.ai.ai;

import com.steve.ai.SteveMod;
import com.steve.ai.action.Task;
import com.steve.ai.config.SteveConfig;
import com.steve.ai.entity.SteveEntity;
import com.steve.ai.memory.WorldKnowledge;

import java.util.List;

public class TaskPlanner {
    private final OpenAIClient openAIClient;
    private final GeminiClient geminiClient;
    private final GroqClient groqClient;

    public TaskPlanner() {
        this.openAIClient = new OpenAIClient();
        this.geminiClient = new GeminiClient();
        this.groqClient = new GroqClient();
    }

    public ResponseParser.ParsedResponse planTasks(SteveEntity steve, String command) {
        try {
            String systemPrompt = PromptBuilder.buildSystemPrompt();
            WorldKnowledge worldKnowledge = new WorldKnowledge(steve);
            String userPrompt = PromptBuilder.buildUserPrompt(steve, command, worldKnowledge);
            
            String provider = SteveConfig.AI_PROVIDER.get().toLowerCase();
            SteveMod.LOGGER.info("Requesting AI plan for Steve '{}' using {}: {}", steve.getSteveName(), provider, command);
            
            String response = getAIResponse(provider, systemPrompt, userPrompt);
            
            if (response == null) {
                SteveMod.LOGGER.error("Failed to get AI response for command: {}", command);
                return null;
            }            ResponseParser.ParsedResponse parsedResponse = ResponseParser.parseAIResponse(response);
            
            if (parsedResponse == null) {
                SteveMod.LOGGER.error("Failed to parse AI response");
                return null;
            }
            
            SteveMod.LOGGER.info("Plan: {} ({} tasks)", parsedResponse.getPlan(), parsedResponse.getTasks().size());
            
            return parsedResponse;
            
        } catch (Exception e) {
            SteveMod.LOGGER.error("Error planning tasks", e);
            return null;
        }
    }

    private String getAIResponse(String provider, String systemPrompt, String userPrompt) {
        String response = switch (provider) {
            case "groq" -> groqClient.sendRequest(systemPrompt, userPrompt);
            case "gemini" -> geminiClient.sendRequest(systemPrompt, userPrompt);
            case "openai" -> openAIClient.sendRequest(systemPrompt, userPrompt);
            default -> {
                SteveMod.LOGGER.warn("Unknown AI provider '{}', using Groq", provider);
                yield groqClient.sendRequest(systemPrompt, userPrompt);
            }
        };
        
        if (response == null && !provider.equals("groq")) {
            SteveMod.LOGGER.warn("{} failed, trying Groq as fallback", provider);
            response = groqClient.sendRequest(systemPrompt, userPrompt);
        }
        
        return response;
    }

    public boolean validateTask(Task task) {
        String action = task.getAction();
        
        return switch (action) {
            case "pathfind" -> task.hasParameters("x", "y", "z");
            case "mine" -> task.hasParameters("block", "quantity");
            case "place" -> task.hasParameters("block", "x", "y", "z");
            case "craft" -> task.hasParameters("item", "quantity");
            case "attack" -> task.hasParameters("target");
            case "follow" -> task.hasParameters("player");
            case "gather" -> task.hasParameters("resource", "quantity");
            case "build" -> task.hasParameters("structure", "blocks", "dimensions");
            default -> {
                SteveMod.LOGGER.warn("Unknown action type: {}", action);
                yield false;
            }
        };
    }

    public List<Task> validateAndFilterTasks(List<Task> tasks) {
        return tasks.stream()
            .filter(this::validateTask)
            .toList();
    }
}


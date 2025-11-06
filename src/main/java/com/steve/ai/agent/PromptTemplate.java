package com.steve.ai.agent;

import java.util.Map;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

// TODO: Will be implemented later - dynamic prompt templating system
// Currently prompts are hardcoded in PromptBuilder using text blocks
// This provides placeholder infrastructure for variable prompt templates
public class PromptTemplate {
    private final String template;
    private final String[] inputVariables;
    
    public PromptTemplate(String template, String... inputVariables) {
        this.template = template;
        this.inputVariables = inputVariables;
    }
    
    public String format(Map<String, Object> values) {
        String result = template;
        
        for (String variable : inputVariables) {
            Object value = values.get(variable);
            if (value != null) {
                String placeholder = "{" + variable + "}";
                result = result.replace(placeholder, value.toString());
            }
        }
        
        return result;
    }
    
    public static PromptTemplate fromTemplate(String template) {
        Pattern pattern = Pattern.compile("\\{([^}]+)\\}");
        Matcher matcher = pattern.matcher(template);
        
        java.util.List<String> variables = new java.util.ArrayList<>();
        while (matcher.find()) {
            variables.add(matcher.group(1));
        }
        
        return new PromptTemplate(template, variables.toArray(new String[0]));
    }
}

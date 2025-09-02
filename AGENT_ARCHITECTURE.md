# Steve AI Agent Architecture

## Overview

Steve AI uses a LangChain-inspired agent architecture with the ReAct (Reasoning + Acting) framework for autonomous decision-making in Minecraft.

## Core Components

### 1. Agent Chain (`AgentChain.java`)
- Orchestrates the reasoning and action loop
- Manages chain state across multiple steps
- Builds context from Steve's memory and environment

### 2. Agent Executor (`AgentExecutor.java`)
- Executes agent decisions
- Manages tool selection and invocation
- Handles max iteration limits and error recovery

### 3. ReAct Agent (`ReActAgent.java`)
- Implements the ReAct reasoning framework
- Generates thoughts based on observations
- Selects actions using thought history
- Synthesizes final answers from action results

### 4. Tools (`ToolWrapper.java`)
Available tools for agent interaction:
- **Build**: Structure construction
- **Mine**: Resource gathering
- **Attack**: Combat actions
- **Pathfind**: Navigation

### 5. Memory Systems

#### Conversational Memory (`ConversationalMemory.java`)
- Stores chat history with timestamps
- Maintains user/assistant message pairs
- Token-limited buffer for context management

#### Vector Store (`VectorStore.java`)
- Semantic search over past experiences
- Cosine similarity for relevance ranking
- 384-dimensional embeddings
- Metadata-enriched storage

### 6. Prompt Management (`PromptTemplate.java`)
- Dynamic prompt formatting
- Variable substitution
- Template validation

## Architecture Flow

```
User Input
    ↓
ReActAgent.reason()
    ↓
Generate Thought ← Relevant Memories (Vector Store)
    ↓
Select Action
    ↓
AgentExecutor.execute()
    ↓
ToolWrapper.invoke()
    ↓
Action Result
    ↓
Update Memory
    ↓
Synthesize Answer
```

## Configuration

Agent behavior is controlled via `langchain_config.json`:
- Model selection and parameters
- Memory limits and vector store settings
- Tool definitions
- Prompt templates

## Integration with Minecraft

The agent system integrates with Minecraft through:
- `SteveEntity`: The physical entity in the game
- `ActionExecutor`: Bridges agent decisions to game actions
- `SteveMemory`: Persistent memory across game sessions

## Future Enhancements

- Multi-agent coordination using shared memory
- Long-term episodic memory with summarization
- Tool learning and dynamic tool creation
- Hierarchical planning for complex tasks


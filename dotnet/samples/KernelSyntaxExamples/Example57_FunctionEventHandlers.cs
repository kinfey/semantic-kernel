﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Events;
using RepoUtils;

// ReSharper disable once InconsistentNaming
public static class Example57_FunctionEventHandlers
{
    private static string? openAIModelId;
    private static string? openAIApiKey;

    public static async Task RunAsync()
    {
        Console.WriteLine("\n======== Using Function Execution Handlers ========\n");

        openAIModelId = TestConfiguration.OpenAI.ChatModelId;
        openAIApiKey = TestConfiguration.OpenAI.ApiKey;

        if (openAIModelId == null || openAIApiKey == null)
        {
            Console.WriteLine("OpenAI credentials not found. Skipping example.");
            return;
        }

        await GetUsageAsync();

        await ChangingResultAsync();

        await BeforeInvokeCancellationAsync();

        await AfterInvokeCancellationAsync();

        await SkippingFunctionsAsync();

        await RepeatFunctionsAsync();
    }

    private static async Task GetUsageAsync()
    {
        Console.WriteLine("\n======== Get Rendered Prompt and Usage Data ========\n");

        IKernel kernel = new KernelBuilder()
            .WithLoggerFactory(ConsoleLogger.LoggerFactory)
            .WithOpenAIChatCompletionService(
                modelId: openAIModelId!,
                apiKey: openAIApiKey!)
            .Build();

        const string functionPrompt = "Write a random paragraph about: {{$input}}.";

        var excuseFunction = kernel.CreateSemanticFunction(
            functionPrompt,
            skillName: "MySkill",
            functionName: "Excuse",
            maxTokens: 100,
            temperature: 0.4,
            topP: 1);

        void MyPreHandler(object? sender, FunctionInvokingEventArgs e)
        {
            Console.WriteLine($"{e.FunctionView.SkillName}.{e.FunctionView.Name} : Pre Execution Handler - Triggered");
        }

        void MyRemovedPreExecutionHandler(object? sender, FunctionInvokingEventArgs e)
        {
            Console.WriteLine($"{e.FunctionView.SkillName}.{e.FunctionView.Name} : Pre Execution Handler - Should not trigger");
            e.Cancel();
        }

        void MyPostExecutionHandler(object? sender, FunctionInvokedEventArgs e)
        {
            Console.WriteLine($"{e.FunctionView.SkillName}.{e.FunctionView.Name} : Post Execution Handler - Total Tokens: {e.SKContext.ModelResults.First().GetOpenAIChatResult().Usage.TotalTokens}");
        }

        kernel.FunctionInvoking += MyPreHandler;
        kernel.FunctionInvoked += MyPostExecutionHandler;

        // Adding and Removing a handler
        kernel.FunctionInvoking += MyRemovedPreExecutionHandler;
        kernel.FunctionInvoking -= MyRemovedPreExecutionHandler;

        const string input = "I missed the F1 final race";
        var result = await kernel.RunAsync(input, excuseFunction);
        Console.WriteLine($"Function Result: {result}");
    }

    private static async Task ChangingResultAsync()
    {
        Console.WriteLine("\n======== Changing/Filtering Function Result ========\n");

        IKernel kernel = new KernelBuilder()
           .WithLoggerFactory(ConsoleLogger.LoggerFactory)
           .WithOpenAIChatCompletionService(
               modelId: openAIModelId!,
               apiKey: openAIApiKey!)
           .Build();

        const string functionPrompt = "Write a paragraph about Handlers.";

        var writerFunction = kernel.CreateSemanticFunction(
            functionPrompt,
            skillName: "MySkill",
            functionName: "Writer",
            maxTokens: 100,
            temperature: 0.4,
            topP: 1);

        void MyChangeDataHandler(object? sender, FunctionInvokedEventArgs e)
        {
            var originalOutput = e.SKContext.Result;

            //Use Regex to redact all vowels and numbers
            var newOutput = Regex.Replace(originalOutput, "[aeiouAEIOU0-9]", "*");

            e.SKContext.Variables.Update(newOutput);
        }

        kernel.FunctionInvoked += MyChangeDataHandler;

        var result = await kernel.RunAsync(writerFunction);

        Console.WriteLine($"Function Result: {result}");
    }

    private static async Task BeforeInvokeCancellationAsync()
    {
        Console.WriteLine("\n======== Cancelling Pipeline Execution - Invoking event ========\n");

        IKernel kernel = new KernelBuilder()
           .WithLoggerFactory(ConsoleLogger.LoggerFactory)
           .WithOpenAIChatCompletionService(
               modelId: openAIModelId!,
               apiKey: openAIApiKey!)
           .Build();

        const string functionPrompt = "Write a paragraph about: Cancellation.";

        var writerFunction = kernel.CreateSemanticFunction(
            functionPrompt,
            skillName: "MySkill",
            functionName: "Writer",
            maxTokens: 1000,
            temperature: 1,
            topP: 0.5);

        // Adding new inline handler to cancel/prevent function execution
        kernel.FunctionInvoking += (object? sender, FunctionInvokingEventArgs e) =>
        {
            Console.WriteLine($"{e.FunctionView.SkillName}.{e.FunctionView.Name} : FunctionInvoking - Cancelling all subsequent invocations");
            e.Cancel();
        };

        // Technically invoked will never be called since the function will be cancelled
        int functionInvokedCount = 0;
        kernel.FunctionInvoked += (object? sender, FunctionInvokedEventArgs e) =>
        {
            functionInvokedCount++;
        };

        var result = await kernel.RunAsync(writerFunction);
        Console.WriteLine($"Function Invocation Times: {functionInvokedCount}");
    }

    private static async Task AfterInvokeCancellationAsync()
    {
        Console.WriteLine("\n======== Cancelling Pipeline Execution - Invoked event ========\n");

        IKernel kernel = new KernelBuilder()
           .WithLoggerFactory(ConsoleLogger.LoggerFactory)
           .WithOpenAIChatCompletionService(
               modelId: openAIModelId!,
               apiKey: openAIApiKey!)
           .Build();

        int functionInvokingCount = 0;
        int functionInvokedCount = 0;

        var firstFunction = kernel.CreateSemanticFunction("Write a phrase with Invoke.", functionName: "InvokePhrase");
        var secondFunction = kernel.CreateSemanticFunction("Write a phrase with Cancellation.", functionName: "CancellationPhrase");

        // Adding new inline handler to count invoking events
        kernel.FunctionInvoking += (object? sender, FunctionInvokingEventArgs e) =>
        {
            functionInvokingCount++;
        };

        // Invoked will never be called twice (for the secondFunction) since Invoked from the first is cancelling.
        kernel.FunctionInvoked += (object? sender, FunctionInvokedEventArgs e) =>
        {
            functionInvokedCount++;
            e.Cancel();
        };

        var result = await kernel.RunAsync(secondFunction);
        Console.WriteLine($"Function Invoked Times: {functionInvokedCount}");
        Console.WriteLine($"Function Invoking Times: {functionInvokingCount}");
    }

    private static async Task SkippingFunctionsAsync()
    {
        Console.WriteLine("\n======== Skipping a Function in the Pipeline ========\n");

        IKernel kernel = new KernelBuilder()
           .WithLoggerFactory(ConsoleLogger.LoggerFactory)
           .WithOpenAIChatCompletionService(
               modelId: openAIModelId!,
               apiKey: openAIApiKey!)
           .Build();

        var skipMeFunction = kernel.CreateSemanticFunction("Write a paragraph about Skipping",
            skillName: "MySkill",
            functionName: "SkipMe");

        var dontSkipMeFunction = kernel.CreateSemanticFunction("Write a paragraph about Handlers",
            skillName: "MySkill",
            functionName: "DontSkipMe");

        kernel.FunctionInvoking += (object? sender, FunctionInvokingEventArgs e) =>
        {
            if (e.FunctionView.Name == "SkipMe")
            {
                e.Skip();
                Console.WriteLine($"Function {e.FunctionView.Name} will be skipped");
                return;
            }

            Console.WriteLine($"Function {e.FunctionView.Name} will not be skipped");
        };

        kernel.FunctionInvoked += (object? sender, FunctionInvokedEventArgs e) =>
        {
            Console.WriteLine($"Only not skipped functions will trigger invoked event - Function name: {e.FunctionView.Name}");
        };

        var context = await kernel.RunAsync(
            skipMeFunction,
            dontSkipMeFunction);

        Console.WriteLine($"Final result: {context.Result}");
    }

    private static async Task RepeatFunctionsAsync()
    {
        Console.WriteLine("\n======== Repeating a Function in the Pipeline ========");

        IKernel kernel = new KernelBuilder()
           .WithLoggerFactory(ConsoleLogger.LoggerFactory)
           .WithOpenAIChatCompletionService(
               modelId: openAIModelId!,
               apiKey: openAIApiKey!)
           .Build();

        var repeatSubjects = new Queue<string>(new[] { "Life", "Work", "Leisure" });

        var repeatMeFunction = kernel.CreateSemanticFunction("Write a sentence about {{$input}}",
            skillName: "MySkill",
            functionName: "RepeatMe");

        var repeatTimes = 0;
        kernel.FunctionInvoked += (object? sender, FunctionInvokedEventArgs e) =>
        {
            Console.WriteLine($"\nFunction {e.FunctionView.Name} executed:");
            Console.WriteLine($"Result: {e.SKContext.Result}");

            if (repeatTimes < 3)
            {
                // Flag the Kernel to repeat the function
                e.Repeat();

                // Redefine the input variable to repeat the function
                e.SKContext.Variables.Update(repeatSubjects.Dequeue());

                repeatTimes++;
                Console.WriteLine("Repeat requested!");

                return;
            }
        };

        var context = await kernel.RunAsync("Repetition", repeatMeFunction);
    }
}

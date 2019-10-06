﻿using Converto;
using System.Collections.Generic;
using System.Linq;
using static ReduxSimple.Reducers;
using static ReduxSimple.Uwp.Samples.TodoList.Entities;

namespace ReduxSimple.Uwp.Samples.TodoList
{
    public static class Reducers
    {
        public static IEnumerable<On<TodoListState>> CreateReducers()
        {
            return new List<On<TodoListState>>
            {
                On<SetFilterAction, TodoListState>(
                    (state, action) => state.With(new { action.Filter })
                ),
                On<CreateTodoItemAction, TodoListState>(
                    state =>
                    {
                        int newId = state.Items.Collection.Any() ? state.Items.Ids.Max() + 1 : 1;
                        return state.With(new
                        {
                            Items = TodoItemAdapter.UpsertOne(new TodoItem { Id = newId }, state.Items)
                        });
                    }
                ),
                On<CompleteTodoItemAction, TodoListState>(
                    (state, action) =>
                    {
                        var itemToUpdate = state.Items.Collection[action.Id]; // TODO : Use Partial<T> upsert
                        return state.With(new
                        {
                            Items = TodoItemAdapter.UpsertOne(itemToUpdate.With(new { Completed = true }), state.Items)
                        });
                    }
                ),
                On<RevertCompleteTodoItemAction, TodoListState>(
                    (state, action) =>
                    {
                        var itemToUpdate = state.Items.Collection[action.Id]; // TODO : Use Partial<T> upsert
                        return state.With(new
                        {
                            Items = TodoItemAdapter.UpsertOne(itemToUpdate.With(new { Completed = false }), state.Items)
                        });
                    }
                ),
                On<UpdateTodoItemAction, TodoListState>(
                    (state, action) =>
                    {
                        var itemToUpdate = state.Items.Collection[action.Id]; // TODO : Use Partial<T> upsert
                        return state.With(new
                        {
                            Items = TodoItemAdapter.UpsertOne(itemToUpdate.With(new { action.Content }), state.Items)
                        });
                    }
                ),
                On<RemoveTodoItemAction, TodoListState>(
                    (state, action) =>
                    {
                        return state.With(new
                        {
                            Items = TodoItemAdapter.RemoveOne(action.Id, state.Items)
                        });
                    }
                )
            };
        }
    }
}

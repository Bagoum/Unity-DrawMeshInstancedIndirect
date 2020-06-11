using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A pool for acquiring compute buffers without repeated reallocation.
/// This class should be used by managers rendering many objects to screen.
/// A manager should create its own buffer pool. It should call Flush on the pool
/// right before it starts rendering. Then, it should repeatedly Rent buffers
/// for each of its render calls.
/// </summary>
public class ComputeBufferPool : IDisposable {
    private readonly int count;
    private readonly int stride;
    private readonly ComputeBufferType cbt;
    private readonly Stack<ComputeBuffer> free = new Stack<ComputeBuffer>();
    private readonly Stack<ComputeBuffer> active = new Stack<ComputeBuffer>();

    public ComputeBufferPool(int batchSize, int stride, ComputeBufferType typ) {
        count = batchSize;
        this.stride = stride;
        cbt = typ;
    }

    public ComputeBuffer Rent() {
        ComputeBuffer cb;
        if (free.Count > 0) {
            cb = free.Pop();
        } else {
            cb = new ComputeBuffer(count, stride, cbt);
        }
        active.Push(cb);
        return cb;
    }
    public void Flush() {
        while (active.Count > 0) {
            free.Push(active.Pop());
        }
    }

    public void Dispose() {
        while (active.Count > 0) {
            active.Pop().Dispose();
        }
        while (free.Count > 0) {
            free.Pop().Dispose();
        }
    }
}
using System;

namespace Sharper.C.Control
{

public static class TrampolineModule
{
    public static Trampoline<A> Delay<A>(Func<A> f)
        =>
        Suspend(() => Done(f()));

    public static Trampoline<A> Done<A>(A a)
        =>
        new TrampDone<A>(a);

    public static Trampoline<A> Suspend<A>(Func<Trampoline<A>> k)
        =>
        new TrampSuspend<A>(k);

    public static Trampoline<B> Select<A, B>
      ( this Trampoline<A> ta
      , Func<A, B> f
      )
    =>
        ta.Map(f);

    public static Trampoline<C> SelectMany<A, B, C>
      ( this Trampoline<A> ta
      , Func<A, Trampoline<B>> f
      , Func<A, B, C> g
      )
    =>
        ta.FlatMap(a => f(a).Map(b => g(a, b)));

    public abstract class Trampoline<A>
    {
        public abstract Trampoline<B> FlatMap<B>(Func<A, Trampoline<B>> f);
        public abstract Trampoline<A> Step { get; }
        internal abstract Trampoline<B> BindStep<B>(Func<A, Trampoline<B>> k);

        public A Eval()
        {
            var t = Step;
            for (; !(t is TrampDone<A>); t = t.Step)
            {
            }
            return ((TrampDone<A>) t).Value;
        }

        public Trampoline<B> Map<B>(Func<A, B> f)
        =>
            FlatMap(a => Delay(() => f(a)));
    }

    private sealed class TrampBind<A, B>
      : Trampoline<B>
    {
        public Trampoline<A> Sub { get; }
        public Func<A, Trampoline<B>> Cont { get; }

        public TrampBind(Trampoline<A> sub, Func<A, Trampoline<B>> cont)
        {
            Sub = sub;
            Cont = cont;
        }

        public override Trampoline<C> FlatMap<C>(Func<B, Trampoline<C>> f)
        =>
            new TrampBind<A, C>(Sub, a => Cont(a).FlatMap(f));

        public override Trampoline<B> Step
        =>
            Sub.BindStep(Cont);

        internal override Trampoline<C> BindStep<C>(Func<B, Trampoline<C>> k)
        =>
            Sub.FlatMap(a => Cont(a).FlatMap(k));
    }

    private sealed class TrampDone<A>
      : Trampoline<A>
    {
        public A Value { get; }

        public TrampDone(A a)
        {
            Value = a;
        }

        public override Trampoline<B> FlatMap<B>(Func<A, Trampoline<B>> f)
        =>
            new TrampBind<A, B>(this, f);

        public override Trampoline<A> Step
        =>
            this;

        internal override Trampoline<B> BindStep<B>(Func<A, Trampoline<B>> k)
        =>
            k(Value);
    }

    private sealed class TrampSuspend<A>
      : Trampoline<A>
    {
        public Func<Trampoline<A>> Cont { get; }

        public TrampSuspend(Func<Trampoline<A>> cont)
        {
            Cont = cont;
        }

        public override Trampoline<B> FlatMap<B>(Func<A, Trampoline<B>> f)
        =>
            new TrampBind<A, B>(this, f);

        public override Trampoline<A> Step
        =>
            Cont();

        internal override Trampoline<B> BindStep<B>(Func<A, Trampoline<B>> k)
        =>
            Cont().FlatMap(k);
    }
}

}

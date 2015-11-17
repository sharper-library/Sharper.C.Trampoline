using System;
using FsCheck;
using Fuchu;

namespace Sharper.C.Testing
{

using static Control.StepModule;

public static class StepTestingModule
{
    public static Arbitrary<Step<A>> AnyStep<A>(Arbitrary<A> arbA)
    =>
        arbA.Convert(Done, s => s.Eval());

    public static Test WithoutOverflow<A>(this string label, Func<Step<A>> f)
    =>
        label.Group
          ( Test.Case("Build", () => f())
          , Test.Case("Evaluate", () => f().Eval())
          );
}

}

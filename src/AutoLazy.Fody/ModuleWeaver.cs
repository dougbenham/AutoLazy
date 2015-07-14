﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace AutoLazy.Fody
{
    public class ModuleWeaver
    {
        public ModuleDefinition ModuleDefinition { get; set; }

        public Action<string> LogError { get; set; }

        public Action<string, SequencePoint> LogErrorPoint { get; set; }

        public void Execute()
        {
            foreach (var method in GetMethods().ToList())
            {
                Instrument(method);
            }
        }

        private void LogMethodError(string message, MethodDefinition method)
        {
            var sequencePoint = method.Body.Instructions.Select(i => i.SequencePoint).FirstOrDefault();
            if (sequencePoint == null)
            {
                LogError(string.Format("{0} - see {1}.{2}", message, method.DeclaringType.FullName, method.Name));
            }
            else
            {
                LogErrorPoint(message, sequencePoint);
            }
        }

        private void Instrument(MethodDefinition method)
        {
            if (IsValid(method))
            {
                DoubleCheckedLockingWeaver.Instrument(method);
            }
        }

        private bool IsValid(MethodDefinition method)
        {
            var valid = true;
            if (method.Parameters.Count > 0)
            {
                LogMethodError("[Lazy] methods may not have any parameters.", method);
                valid = false;
            }
            if (method.ReturnType.MetadataType == MetadataType.Void)
            {
                LogMethodError("[Lazy] methods must have a non-void return type.", method);
                valid = false;
            }
            return valid;
        }

        private IEnumerable<MethodDefinition> GetMethods()
        {
            return from type in ModuleDefinition.Types
                   from method in type.Methods
                   let attribute = GetLazyAttribute(method)
                   where attribute != null
                   select method;
        }

        private static CustomAttribute GetLazyAttribute(ICustomAttributeProvider method)
        {
            return method.CustomAttributes.FirstOrDefault(attr => attr.AttributeType.FullName == "AutoLazy.LazyAttribute");
        }
    }
}
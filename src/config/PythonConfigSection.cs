namespace Python.Config
{
    using System;
    using System.Configuration;

    public class PythonConfigSection : ConfigurationSection
    {
        [ConfigurationProperty("pythonVersion", DefaultValue = "2.7", IsRequired = true)]
        public string PythonVersion
        {
            get
            {
                return (string)this["pythonVersion"];
            }

            set
            {
                this["pythonVersion"] = value;
            }
        }
    }
}
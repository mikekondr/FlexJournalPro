namespace FlexJournalPro.Services
{
    public interface IKeyManagementService
    {
        bool HasDpapiError();
        string GetDecryptedDekString();
        bool UnlockDekWithPassword(string login, string password);
        void SetOrUpdateUserKey(string login, string userPassword);
        string ExportMasterRecoveryKey();
        //void RecoverWithMasterKey(string base64Dek, string login, string newPassword);
        void RemoveUserKey(string login);
        void ClearKeystore();
        void GenerateMasterKeyInMemory();
        void SetMasterKeyInMemory(string base64Dek);
    }
}